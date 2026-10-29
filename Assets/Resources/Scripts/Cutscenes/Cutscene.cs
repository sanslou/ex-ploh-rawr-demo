using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

public class Cutscene : MonoBehaviour
{
    // Note: This class is responsible for playing a cutscene: a list of actions (dialogue, movement, fades, camera switches) that run one after another chronologically.

    public enum CutsceneActionType
    {
        Dialogue,
        MoveTo,
        FaceTarget,
        Wait,
        FadeBlack,
        SwitchCamera,
        Teleport,
        MoveAndTalk,
        InvokeEvent
    }

    public enum CharacterTarget
    {
        NPC,
        Player
    }

    [Serializable]
    public class CutsceneAction
    {
        public CutsceneActionType actionType;

        [Header("Character / Movement Target")]
        public CharacterTarget characterTarget = CharacterTarget.NPC;
        public NPC npc;

        [Header("Dialogue")]
        [TextArea(2, 5)]
        public string dialogue;
        public bool autoAdvanceDialogue = false;

        [Header("MoveAndTalk Speaker")]
        public CharacterTarget speakerTarget = CharacterTarget.NPC;
        [Tooltip("Optional separate NPC speaker. Leave empty to use NPC above.")]
        public NPC speakerNPC;

        [Header("Voice Line")]
        public AudioClip voiceLine;
        [Range(0f, 1f)]
        public float voiceVolume = 1f;

        [Header("Movement / Teleport")]
        public Transform destination;
        public float moveSpeed = 3f;
        public float stoppingDistance = 0.2f;

        [Header("Facing")]
        public Transform faceTarget;

        [Header("Wait")]
        public float waitSeconds = 1f;

        [Header("Fade Black")]
        [Range(0f, 1f)]
        public float targetAlpha = 1f;
        public float fadeDuration = 1.5f;

        [Header("Switch Camera")]
        public Camera targetCamera;

        [Header("Custom Event")]
        public UnityEvent onActionInvoked = new UnityEvent();
    }

    [Header("Player UI")]
    [SerializeField] private GameObject UIPlayer;

    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceAudioSource;

    [Header("Cutscene")]
    public bool playOnStart = true;
    [SerializeField] private bool autoAdvanceBeforeFade = true; // Skips the Next button when the next action is a fade
    [SerializeField] private List<CutsceneAction> actions = new List<CutsceneAction>();

    [Header("Black Screen")]
    [SerializeField] private Image blackScreen;

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private bool restoreGameplayCamera = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onCutsceneFinished = new UnityEvent();

    [Header("Cutscene Completion")]
    [SerializeField] private string cutsceneId;
    [SerializeField] private string progressMilestoneId;

    private string progressionProfile;

    private bool previouslyFinished;
    private string watchedSaveKey;

    // Fetch References
    private GameObject joystickBG;
    private GameObject buttonPrev;
    private GameObject buttonInteract;
    private Button buttonNext;
    private TMP_InputField dialogueText;
    private Camera currentCamera;

    // Remembers the player UI so we can put it back after the cutscene
    private bool previousJoystickState;
    private bool previousPrevState;
    private bool previousNextState;
    private bool previousInteractState;
    private bool previousNextInteractable;
    private bool uiHidden;

    private bool isPlaying;
    private bool waitingForContinue;
    private bool continueRequested;

    // Protects the cutscene dialogue from NPC trigger scripts
    private bool cutsceneDialogueVisible = false;
    private string displayedCutsceneDialogue = "";

    public bool IsPlaying
    {
        get { return isPlaying; }
    }

    void Awake()
    {
        if (voiceAudioSource == null) voiceAudioSource = GetComponent<AudioSource>();
        if (voiceAudioSource == null) voiceAudioSource = gameObject.AddComponent<AudioSource>();

        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;
        voiceAudioSource.spatialBlend = 0f;
    }

    void Start()
    {
        findPlayerUI();
        findPlayer();
        initializeCamera();

        if (buttonNext != null)
        {
            buttonNext.onClick.AddListener(ContinueDialogue);
        }

        if (blackScreen != null)
        {
            blackScreen.enabled = true;
            blackScreen.raycastTarget = false;
        }

        if (playOnStart)
        {
            PlayCutscene();
        }
    }

    void LateUpdate()
    {
        if (!isPlaying)
            return;
        // Stops interference when the quiz has disabled the player Canvas.
        if (UIPlayer != null)
        {
            Canvas playerCanvas = UIPlayer.GetComponent<Canvas>();

            if (playerCanvas != null && !playerCanvas.enabled)
                return;
        }

        // Keep the gameplay controls hidden
        if (joystickBG != null && joystickBG.activeSelf) joystickBG.SetActive(false);
        if (buttonInteract != null && buttonInteract.activeSelf) buttonInteract.SetActive(false);
        if (buttonPrev != null && buttonPrev.activeSelf) buttonPrev.SetActive(false);

        if (!cutsceneDialogueVisible)
            return;

        /*
        // Restore the Canvas if another script disables it
        if (UIPlayer != null)
        {
            Canvas playerCanvas = UIPlayer.GetComponent<Canvas>();

            if (playerCanvas != null && !playerCanvas.enabled)
            {
                playerCanvas.enabled = true;
            }
        }*/

        // Restore the dialogue UI if an NPC's exit trigger hides it
        if (dialogueText != null)
        {
            if (!dialogueText.gameObject.activeSelf) dialogueText.gameObject.SetActive(true);
            if (!dialogueText.enabled) dialogueText.enabled = true;

            Image image = dialogueText.GetComponent<Image>();

            if (image != null && !image.enabled)
            {
                image.enabled = true;
            }

            // Restore the text if another script clears it
            if (dialogueText.text != displayedCutsceneDialogue)
            {
                dialogueText.text = displayedCutsceneDialogue;
            }
        }

        // Next button is only visible while waiting for the player to continue
        if (buttonNext != null)
        {
            if (waitingForContinue)
            {
                if (!buttonNext.gameObject.activeSelf) buttonNext.gameObject.SetActive(true);
                if (!buttonNext.interactable) buttonNext.interactable = true;
            }
            else if (buttonNext.gameObject.activeSelf)
            {
                buttonNext.gameObject.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (buttonNext != null)
        {
            buttonNext.onClick.RemoveListener(ContinueDialogue);
        }
    }

    void OnDisable()
    {
        if (isPlaying)
        {
            StopAllCoroutines();
            finishCutscene(false);
        }
    }

    void findPlayer()
    {
        if (player != null)
            return;

        PlayerInteract playerScript = FindFirstObjectByType<PlayerInteract>();

        if (playerScript != null)
        {
            player = playerScript.transform;
        }
    }

    void findPlayerUI()
    {
        if (UIPlayer == null)
        {
            UIPlayer = GameObject.FindGameObjectWithTag("Canvas_Player");
        }

        if (UIPlayer == null)
        {
            Debug.LogError("Cutscene: UI Player not found! (Does the player canvas have the tag \"Canvas_Player\"?)");
            return;
        }

        Transform ui = UIPlayer.transform;

        Transform joystick = ui.Find("Joystick BG");
        Transform prev = ui.Find("Button Prev");
        Transform next = ui.Find("Button Next");
        Transform interact = ui.Find("Button Interact");
        Transform dialogue = ui.Find("Dialogue Text");

        if (joystick != null) joystickBG = joystick.gameObject;
        if (prev != null) buttonPrev = prev.gameObject;
        if (next != null) buttonNext = next.GetComponent<Button>();
        if (interact != null) buttonInteract = interact.gameObject;

        if (dialogue != null)
        {
            dialogueText = dialogue.GetComponent<TMP_InputField>();

            if (dialogueText != null)
            {
                dialogueText.readOnly = true;
            }
        }
    }

    void initializeCamera()
    {
        if (gameplayCamera == null && PlayerInteract.PLAYER_CAMERA != null)
        {
            gameplayCamera = PlayerInteract.PLAYER_CAMERA;
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        if (gameplayCamera != null)
        {
            currentCamera = gameplayCamera;
            PlayerInteract.PLAYER_CAMERA = gameplayCamera;
        }
    }

    public void PlayCutscene()
    {
        if (isPlaying)
            return;

        if (buttonNext == null)
        {
            findPlayerUI();
        }

        if (buttonNext == null)
        {
            Debug.LogError("Cutscene: Button Next is missing! (Is there a child named \"Button Next\" in the player canvas?)");
            return;
        }

        if (player == null)
        {
            findPlayer();
        }

        previouslyFinished = false;
        watchedSaveKey = "";

        if (!string.IsNullOrWhiteSpace(cutsceneId))
        {
            string profile = "guest";

            if (!DBC.IS_GUEST_MODE)
            {
                string accountId = PlayerPrefs.GetString("AccountID", "").Trim();

                if (!string.IsNullOrEmpty(accountId))
                {
                    profile = "student:" + accountId;
                }
            }

            progressionProfile = profile;
            watchedSaveKey = "EXplorer_CutsceneWatched_" +
                Uri.EscapeDataString(profile) + "_" +
                Uri.EscapeDataString(cutsceneId.Trim());

            previouslyFinished = PlayerPrefs.GetInt(watchedSaveKey, 0) == 1;
        }
        else
        {
            Debug.LogWarning("Cutscene: Assign a unique Cutscene ID on " + name);
        }

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence() // Runs every action in the list, one after another
    {
        isPlaying = true;

        // Keep PlayerInteract enabled for gravity
        HidePlayerUI();

        for (int i = 0; i < actions.Count; i++)
        {
            CutsceneAction action = actions[i];

            if (action == null)
                continue;

            waitingForContinue = false;

            if (buttonNext != null)
            {
                buttonNext.gameObject.SetActive(false);
            }

            // 1. Decide if the dialogue moves on by itself
            bool advanceAutomatically = action.autoAdvanceDialogue;

            if (autoAdvanceBeforeFade && i + 1 < actions.Count)
            {
                CutsceneAction nextAction = actions[i + 1];

                if (nextAction != null && nextAction.actionType == CutsceneActionType.FadeBlack)
                {
                    advanceAutomatically = true;
                }
            }

            // 2. Do the action
            switch (action.actionType)
            {
                case CutsceneActionType.Dialogue:
                    yield return StartCoroutine(PlayDialogue(action, advanceAutomatically, false));
                    break;

                case CutsceneActionType.MoveTo:
                    yield return StartCoroutine(MoveCharacter(action));
                    break;

                case CutsceneActionType.MoveAndTalk:
                    yield return StartCoroutine(MoveAndTalk(action, advanceAutomatically));
                    break;

                case CutsceneActionType.Teleport:
                    teleportCharacter(action);
                    break;

                case CutsceneActionType.FaceTarget:
                    faceCharacter(action);
                    break;

                case CutsceneActionType.Wait:
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, action.waitSeconds));
                    break;

                case CutsceneActionType.FadeBlack:
                    yield return StartCoroutine(FadeBlack(action));
                    break;

                case CutsceneActionType.SwitchCamera:
                    switchCamera(action.targetCamera);
                    break;
                case CutsceneActionType.InvokeEvent:
                    action.onActionInvoked.Invoke();
                    break;
            }
        }

        finishCutscene(true);
    }

    IEnumerator MoveAndTalk(CutsceneAction action, bool advanceAutomatically) // Walks and talks at the same time
    {
        if (!hasCharacterAndDestination(action, "MoveAndTalk"))
            yield break;

        Coroutine movementRoutine = StartCoroutine(MoveCharacter(action));

        yield return StartCoroutine(PlayDialogue(action, advanceAutomatically, true));

        // Wait for the walking to finish before the next action
        yield return movementRoutine;
    }

    public CutsceneAction GetAction(int index)
    {
        if (index < 0 || index >= actions.Count)
        {
            Debug.LogWarning("Cutscene: Invalid action index: " + index);
            return null;
        }

        return actions[index];
    }
    void finishCutscene(bool completed)
    {
        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
        }

        waitingForContinue = false;
        continueRequested = false;

        if (restoreGameplayCamera)
        {
            restoreCamera();
        }

        hideDialogue();
        Canvas playerCanvas = UIPlayer.GetComponent<Canvas>();

        if (playerCanvas != null && playerCanvas.enabled)
        {
            RestorePlayerUI();
        }
        else
        {
            uiHidden = false;
        }

        isPlaying = false;

        if (completed)
        {
            if (!string.IsNullOrEmpty(watchedSaveKey))
            {
                PlayerPrefs.SetInt(watchedSaveKey, 1);
                PlayerPrefs.Save();

                if (!string.IsNullOrWhiteSpace(progressMilestoneId))
                {
                    ChapterProgression.Complete(
                        progressionProfile,
                        progressMilestoneId.Trim()
                    );
                }
            }

            onCutsceneFinished.Invoke();
        }

    }

    public void HidePlayerUI()
    {
        if (uiHidden)
            return;

        uiHidden = true;

        if (joystickBG != null)
        {
            previousJoystickState = joystickBG.activeSelf;
            MobileController joystick = joystickBG.GetComponent<MobileController>();

            if (joystick != null)
            {
                joystick.ResetInput();
            }
            joystickBG.SetActive(false);
        }

        if (buttonPrev != null)
        {
            previousPrevState = buttonPrev.activeSelf;
            buttonPrev.SetActive(false);
        }

        if (buttonNext != null)
        {
            previousNextState = buttonNext.gameObject.activeSelf;
            previousNextInteractable = buttonNext.interactable;
            buttonNext.gameObject.SetActive(false);
        }

        if (buttonInteract != null)
        {
            previousInteractState = buttonInteract.activeSelf;
            buttonInteract.SetActive(false);
        }
    }

    public void RestorePlayerUI()
    {
        if (!uiHidden)
            return;

        if (joystickBG != null) joystickBG.SetActive(previousJoystickState);
        if (buttonPrev != null) buttonPrev.SetActive(previousPrevState);
        if (buttonInteract != null) buttonInteract.SetActive(previousInteractState);

        if (buttonNext != null)
        {
            buttonNext.interactable = previousNextInteractable;
            buttonNext.gameObject.SetActive(previousNextState);
        }

        uiHidden = false;
    }

    void showDialogue()
    {
        cutsceneDialogueVisible = true;

        if (dialogueText == null)
            return;

        if (!dialogueText.gameObject.activeSelf)
        {
            dialogueText.gameObject.SetActive(true);
        }

        dialogueText.enabled = true;

        Image image = dialogueText.GetComponent<Image>();
        if (image != null) image.enabled = true;
    }

    void hideDialogue()
    {
        cutsceneDialogueVisible = false;
        displayedCutsceneDialogue = "";

        if (dialogueText == null)
            return;

        dialogueText.text = "";

        Image image = dialogueText.GetComponent<Image>();
        if (image != null) image.enabled = false;

        dialogueText.enabled = false;
    }

    public void ContinueDialogue() // Next button
    {
        if (waitingForContinue)
        {
            continueRequested = true;
        }
    }

    IEnumerator PlayDialogue(CutsceneAction action, bool advanceAutomatically, bool useSeparateSpeaker)
    {
        if (string.IsNullOrEmpty(action.dialogue))
            yield break;

        if (dialogueText == null)
        {
            Debug.LogError("Cutscene: Dialogue Text UI is missing! (Is there a child named \"Dialogue Text\" in the player canvas?)");
            yield break;
        }

        continueRequested = false;
        waitingForContinue = false;

        // 1. Find out who is speaking
        CharacterTarget speaker = action.characterTarget;
        NPC speakingNPC = action.npc;

        if (useSeparateSpeaker)
        {
            speaker = action.speakerTarget;

            if (action.speakerNPC != null)
            {
                speakingNPC = action.speakerNPC;
            }
        }

        if (speaker == CharacterTarget.NPC && speakingNPC == null)
        {
            Debug.LogWarning("Cutscene: NPC speaker is not assigned! (Did you drag an NPC into the action?)");
            yield break;
        }

        showDialogue();

        // 2. Play the voice line if there is one
        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
            voiceAudioSource.clip = null;

            if (action.voiceLine != null)
            {
                voiceAudioSource.clip = action.voiceLine;
                voiceAudioSource.volume = Mathf.Clamp01(action.voiceVolume);
                voiceAudioSource.Play();
            }
        }

        // 3. Type the text. The cutscene does its own typing, NPC.Speak() is not called
        yield return StartCoroutine(TypePlayerDialogue(action.dialogue));

        // 4. Wait until the voice line finishes
        if (!previouslyFinished &&
            voiceAudioSource != null &&
            action.voiceLine != null)
        {
            while (voiceAudioSource.isPlaying)
            {
                yield return null;
            }
        }

        // 5. Wait for the Next button, unless it advances by itself
        if (!advanceAutomatically)
        {
            waitingForContinue = true;
            continueRequested = false;

            if (buttonNext != null)
            {
                buttonNext.gameObject.SetActive(true);
                buttonNext.interactable = true;
            }

            while (!continueRequested)
            {
                yield return null;
            }

            waitingForContinue = false;
            continueRequested = false;

            if (buttonNext != null)
            {
                buttonNext.gameObject.SetActive(false);
            }
        }

        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
        }
    }

    IEnumerator TypePlayerDialogue(string message) // Typewriter effect
    {
        showDialogue();

        displayedCutsceneDialogue = "";
        dialogueText.text = "";

        foreach (char letter in message)
        {
            displayedCutsceneDialogue += letter;
            dialogueText.text = displayedCutsceneDialogue;

            yield return new WaitForSeconds(0.01f);
        }
    }

    Transform getCharacter(CutsceneAction action) // The Player or the NPC this action is about
    {
        if (action.characterTarget == CharacterTarget.Player)
        {
            if (player == null)
            {
                findPlayer();
            }

            return player;
        }

        if (action.npc != null)
        {
            return action.npc.transform;
        }

        return null;
    }

    bool hasCharacterAndDestination(CutsceneAction action, string actionName) // One shared warning for Teleport, MoveTo and MoveAndTalk
    {
        if (getCharacter(action) == null || action.destination == null)
        {
            Debug.LogWarning($"Cutscene: {actionName} is missing a character or destination! (Did you assign the NPC and the Destination?)");
            return false;
        }

        return true;
    }

    void teleportCharacter(CutsceneAction action)
    {
        if (!hasCharacterAndDestination(action, "Teleport"))
            return;

        Transform character = getCharacter(action);
        Vector3 position = action.destination.position;

        CharacterController controller = character.GetComponent<CharacterController>();

        if (controller != null)
        {
            // CharacterController has to be off, otherwise it pulls the character back
            bool wasEnabled = controller.enabled;

            controller.enabled = false;
            character.position = position;
            controller.enabled = wasEnabled;
        }
        else
        {
            NavMeshAgent agent = character.GetComponent<NavMeshAgent>();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (!agent.Warp(position))
                    return;

                agent.ResetPath();
            }
            else
            {
                character.position = position;
            }
        }

        // Save the player's new spot
        if (action.characterTarget == CharacterTarget.Player)
        {
            PlayerPrefs.SetFloat("PlayerX", character.position.x);
            PlayerPrefs.SetFloat("PlayerY", character.position.y);
            PlayerPrefs.SetFloat("PlayerZ", character.position.z);
            PlayerPrefs.Save();
        }
    }

    IEnumerator MoveCharacter(CutsceneAction action)
    {
        if (!hasCharacterAndDestination(action, "MoveTo"))
            yield break;

        Transform character = getCharacter(action);

        float speed = Mathf.Max(0.1f, action.moveSpeed);
        float stopDistance = Mathf.Max(0.05f, action.stoppingDistance);

        NavMeshAgent agent = character.GetComponent<NavMeshAgent>();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // NavMeshAgent movement (NPCs)
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            agent.isStopped = false;

            if (!agent.SetDestination(action.destination.position))
            {
                Debug.LogWarning("Cutscene: NavMesh destination failed! (Is the destination on the baked NavMesh?)");
                agent.isStopped = true;
                yield break;
            }

            float elapsed = 0f;

            while (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                elapsed += Time.deltaTime;

                if (elapsed > 30f) // Gives up so the cutscene never freezes
                    break;

                if (!agent.pathPending)
                {
                    if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.pathStatus == NavMeshPathStatus.PathPartial)
                        break;

                    if (agent.remainingDistance <= agent.stoppingDistance)
                    {
                        if (!agent.hasPath || agent.velocity.sqrMagnitude <= 0.01f)
                            break;
                    }
                }

                yield return null;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
        else
        {
            // Plain movement (Player or NPC without a NavMeshAgent)
            CharacterController controller = character.GetComponent<CharacterController>();

            float elapsed = 0f;

            while (character != null && action.destination != null)
            {
                Vector3 direction = action.destination.position - character.position;

                if (action.characterTarget == CharacterTarget.Player)
                {
                    direction.y = 0f; // Player stays on the ground
                }

                if (direction.magnitude <= stopDistance)
                    break;

                elapsed += Time.deltaTime;

                if (elapsed > 30f)
                    break;

                Vector3 movement = Vector3.MoveTowards(Vector3.zero, direction, speed * Time.deltaTime);

                if (controller != null && controller.enabled)
                {
                    controller.Move(movement);
                }
                else
                {
                    character.position += movement;
                }

                yield return null;
            }
        }
    }

    IEnumerator FadeBlack(CutsceneAction action)
    {
        if (blackScreen == null)
        {
            Debug.LogWarning("Cutscene: Black Screen Image not assigned! (Did you assign it in the Inspector?)");
            yield break;
        }

        blackScreen.enabled = true;
        blackScreen.raycastTarget = false;

        float duration = Mathf.Max(0f, action.fadeDuration);

        Color color = blackScreen.color;
        float startAlpha = color.a;
        float endAlpha = Mathf.Clamp01(action.targetAlpha);

        if (duration <= 0f) // No duration, so jump straight to the end
        {
            color.a = endAlpha;
            blackScreen.color = color;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // Smooth in and out

            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            blackScreen.color = color;

            yield return null;
        }

        color.a = endAlpha;
        blackScreen.color = color;
    }

    void switchCamera(Camera targetCamera)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("Cutscene: Target Camera not assigned! (Did you assign it in the SwitchCamera action?)");
            return;
        }
        Debug.Log("Switch cam: " + targetCamera);

        // 1. Only one AudioListener can be on, so turn them all off first
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (AudioListener listener in listeners)
        {
            if (listener != null)
            {
                listener.enabled = false;
            }
        }

        // 2. Ensure only the target camera is enabled and active
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera cam in allCameras)
        {
            if (cam == targetCamera)
            {
                cam.enabled = true;
                cam.gameObject.SetActive(true);
            }
            else
            {
                cam.enabled = false;
                cam.gameObject.SetActive(false);
            }
        }

        // 3. Give the new camera an AudioListener
        AudioListener targetListener = targetCamera.GetComponent<AudioListener>();

        if (targetListener == null)
        {
            targetListener = targetCamera.gameObject.AddComponent<AudioListener>();
        }

        targetListener.enabled = true;

        currentCamera = targetCamera;
        PlayerInteract.PLAYER_CAMERA = targetCamera;
    }

    void restoreCamera()
    {
        if (gameplayCamera != null)
        {
            switchCamera(gameplayCamera);
        }
    }

    void faceCharacter(CutsceneAction action) // Flips the sprite so it looks at the face target
    {
        Transform character = getCharacter(action);

        if (character == null || action.faceTarget == null)
        {
            Debug.LogWarning("Cutscene: FaceTarget is missing a character or target! (Did you assign the NPC and the Face Target?)");
            return;
        }

        SpriteRenderer sprite = null;

        if (action.characterTarget == CharacterTarget.NPC)
        {
            if (action.npc != null)
            {
                sprite = action.npc.npcSprite;
            }
        }
        else
        {
            Transform playerSprite = character.Find("Player Sprite");

            if (playerSprite != null)
            {
                sprite = playerSprite.GetComponent<SpriteRenderer>();
            }
        }

        if (sprite == null)
        {
            Debug.LogWarning("Cutscene: SpriteRenderer not found! (Does the Player have a child named \"Player Sprite\"?)");
            return;
        }

        if (action.faceTarget.position.x > character.position.x)
        {
            sprite.flipX = true;
        }
        else if (action.faceTarget.position.x < character.position.x)
        {
            sprite.flipX = false;
        }
    }
}