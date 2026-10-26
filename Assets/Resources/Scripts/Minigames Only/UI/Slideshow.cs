using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
public class Slideshow : MonoBehaviour
{
    [SerializeField] private bool startSlideshowOnStart = true;
    [SerializeField] private bool pauseGameDuringSlideshow = true;
    [SerializeField] private bool spawnPlayerOnStart = true;
    [SerializeField] private GameObject playerSpawn;
    private PlayerSpawnPoint spawnScript;
    [SerializeField] private UnityEvent onSlideshowFinish;

    [Header("Slideshow UI")]
    [SerializeField] private GameObject ssObject;
    private Canvas slideshowCanvas;

    private TextMeshProUGUI headerText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI indexText;
    private TextMeshProUGUI buttonNextText;

    private Button buttonNext;
    private Button buttonPrev;
    private RawImage imageDisplay;

    [Header("Slideshow Contents")]
    [SerializeField] private string[] headers;
    [TextArea(2,5)]
    [SerializeField] private string[] bodies;
    [Header("Slideshow Images (800x675 recommended)")]
    [SerializeField] private Texture[] images;

    [Header("Tutorial Manager")]
    [SerializeField] private TutorialManager tutorialManager;

    private int currentIndex = 0; // tracks the current slide index

    public int CurrentIndex => currentIndex; // read-only. 

    private void Start()
    {
        if (ssObject == null)
        {
            Debug.LogError("SlideshowUI is not assigned!");
            return;
        }

        headerText = ssObject.transform.Find("Header").GetComponent<TextMeshProUGUI>();
        bodyText = ssObject.transform.Find("Body").GetComponent<TextMeshProUGUI>();
        indexText = ssObject.transform.Find("Index").GetComponent<TextMeshProUGUI>();
        buttonNext = ssObject.transform.Find("Button Next").GetComponent<Button>();
        buttonPrev = ssObject.transform.Find("Button Previous").GetComponent<Button>();
        imageDisplay = ssObject.transform.Find("Image Display").GetComponent<RawImage>();
        buttonNextText = buttonNext.transform.Find("Text").GetComponent<TextMeshProUGUI>();
        slideshowCanvas = ssObject.GetComponent<Canvas>();

        spawnScript = playerSpawn.GetComponent<PlayerSpawnPoint>();
        if (spawnScript == null)
        {
            spawnScript = GameObject.Find("PlayerSpawnPoint").GetComponent<PlayerSpawnPoint>();
        }
        

        buttonNext.onClick.AddListener(NextSlide);
        buttonPrev.onClick.AddListener(PreviousSlide);

        currentIndex = 0;

        if (headers == null || headers.Length == 0)
        {
            Debug.LogError("No slideshow headers assigned!");
            buttonNext.interactable = false;
            buttonPrev.interactable = false;
            return;
        }

        DisplaySlide();
        if (startSlideshowOnStart)
        {
            ShowSlideshow();
        }
        else
        {
            HideSlideshow();
        }
        
        if (startSlideshowOnStart)
        {
            ShowSlideshow();

            if (pauseGameDuringSlideshow)
            {
                Time.timeScale = 0f;
            }
        }
        else
        {
            HideSlideshow();
        }
    }

    private void DisplaySlide() // If necessary, overload this function and add parameter of currentindex to allow for direct slide access by index.
    {
        headerText.text = headers[currentIndex];

        bodyText.text = currentIndex < bodies.Length
            ? bodies[currentIndex]
            : "";

        imageDisplay.texture = currentIndex < images.Length
            ? images[currentIndex]
            : null;

        indexText.text = $"{currentIndex + 1} / {headers.Length}";

        bool isFirstPage = currentIndex == 0;
        bool isLastPage = currentIndex == headers.Length - 1;

        buttonPrev.interactable = !isFirstPage; 

        // Finish remains interactable on the last page.
        buttonNext.interactable = true;
        buttonNextText.text = isLastPage ? "Finish" : "Next";
    }

    public void NextSlide()
    {
        if (currentIndex < headers.Length - 1)
        {
            currentIndex++;
            DisplaySlide();
        }
        else
        {
            HideSlideshow();
            Time.timeScale = 1f; // Resume the game
        }
    }

    public void PreviousSlide()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            DisplaySlide();
        }
    }

    public void GoToSlide(int index)
    {
        if (index >= 0 && index < headers.Length)
        {
            currentIndex = index;
            DisplaySlide();
        }
    }

    public void StartSlideshow() 
    {
        currentIndex = 0;

        DisplaySlide();
        ShowSlideshow();

        if (pauseGameDuringSlideshow)
        {
            Time.timeScale = 0f;
        }
    }
    public void ShowSlideshow()
    {
        ssObject.SetActive(true);
        slideshowCanvas.enabled = true;
    }

    public void HideSlideshow()
    {
        ssObject.SetActive(false);
        slideshowCanvas.enabled = false;
        if (spawnPlayerOnStart)
        {
            spawnScript.TpPlayerToSpawn();
        }
        onSlideshowFinish?.Invoke();
    }

    public void SetSlideshowContents(string[] newHeaders, string[] newBodies, Texture[] newImages) // allows dynamic update of slideshow contents.
    {
        headers = newHeaders;
        bodies = newBodies;
        images = newImages;
        currentIndex = 0; // Reset to the first slide
        DisplaySlide();
    }

}
