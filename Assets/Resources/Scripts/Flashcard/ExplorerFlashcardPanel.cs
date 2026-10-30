using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExplorerFlashcardPanel : MonoBehaviour
{
    [Serializable]
    public class Card
    {
        public int chapter = 1;
        public string term;
        [TextArea] public string definition;
        public bool collected;
    }
    public List<Card> cards = new List<Card>();
    public int chapterCount = 2;
    public Canvas panelCanvas;
    public Text chapterLabel, categoryLabel, sideLabel, cardText, counter;
    public Text collectedLabel, generalLabel;
    public Button previous, next, flip, shuffle;
    [Tooltip("Optional: visible only in Collected. Leave its Inspector OnClick empty.")]
    public Button checkButton;
    private bool wasCanvasVisible;

    private void Awake()
    {
        if (checkButton != null) checkButton.onClick.AddListener(RemoveCurrentCollected);
    }

    private void OnDestroy()
    {
        if (checkButton != null) checkButton.onClick.RemoveListener(RemoveCurrentCollected);
    }

    private void Update()
    {
        bool isVisible = panelCanvas != null && panelCanvas.isActiveAndEnabled;
        if (isVisible && !wasCanvasVisible) Refresh();
        wasCanvasVisible = isVisible;
    }

    public void RemoveCurrentCollected()
    {
        if (!collectedOnly || visible.Count == 0) return;
        int previousIndex = index;
        Card card = visible[index];
        ExplorerFlashcardStore.RemoveCollectedCard(card.chapter, card.term, card.definition);
        Refresh();
        index = Mathf.Clamp(previousIndex, 0, Mathf.Max(0, visible.Count - 1));
        Draw();
    }
    private readonly List<Card> visible = new List<Card>();
    private int chapter = 1, index;
    private bool collectedOnly = true, showingTerm;
    private void Start() { Refresh(); }
    public void Open() { panelCanvas.enabled = true; Refresh(); }
    public void Close() { panelCanvas.enabled = false; }
    public void PreviousChapter() { chapter--; if (chapter < 1) chapter = chapterCount; Refresh(); }
    public void NextChapter() { chapter++; if (chapter > chapterCount) chapter = 1; Refresh(); }
    public void Collected() { collectedOnly = true; Refresh(); }
    public void General() { collectedOnly = false; Refresh(); }
    public void Previous() { if (visible.Count == 0) return; index--; if (index < 0) index = visible.Count - 1; showingTerm = false; Draw(); }
    public void Next() { if (visible.Count == 0) return; index++; if (index >= visible.Count) index = 0; showingTerm = false; Draw(); }
    public void Flip() { showingTerm = !showingTerm; Draw(); }
    public void Shuffle()
    {
        for (int i = visible.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Card temp = visible[i]; visible[i] = visible[j]; visible[j] = temp;
        }
        index = 0; showingTerm = false; Draw();
    }
    public void Refresh()
    {
        // Saved data replaces the old Inspector demo entries at runtime.
        ExplorerFlashcardStore.RegisterLoadedQuizzes();
        cards = ExplorerFlashcardStore.GetCards();
        List<Card> generalCards = ExplorerFlashcardStore.GetGeneralCards();
        visible.Clear();
        int collectedCount = 0, generalCount = 0;
        foreach (Card card in cards)
        {
            if (card == null || card.chapter != chapter || !card.collected) continue;
            collectedCount++;
            if (collectedOnly) visible.Add(card);
        }
        foreach (Card card in generalCards)
        {
            if (card == null || card.chapter != chapter) continue;
            generalCount++;
            if (!collectedOnly) visible.Add(card);
        }
        chapterLabel.text = "CHAPTER " + chapter;
        collectedLabel.text = "Collected Flashcards (" + collectedCount + ")";
        generalLabel.text = "General Flashcards (" + generalCount + ")";
        if (collectedOnly) categoryLabel.text = "COLLECTED FLASHCARDS";
        else categoryLabel.text = "GENERAL FLASHCARDS";
        index = 0; showingTerm = false; Draw();
    }
    private void Draw()
    {
        bool hasCards = visible.Count > 0;
        if (checkButton != null)
        {
            checkButton.gameObject.SetActive(collectedOnly);
            checkButton.interactable = collectedOnly && hasCards;
        }
        previous.interactable = visible.Count > 1; next.interactable = visible.Count > 1;
        flip.interactable = hasCards; shuffle.interactable = visible.Count > 1;
        if (!hasCards)
        {
            sideLabel.text = "EMPTY";
            cardText.text = "No flashcards in this category for this chapter yet.";
            counter.text = "0 / 0"; return;
        }
        if (showingTerm) { sideLabel.text = "TERM"; cardText.text = visible[index].term; }
        else { sideLabel.text = "DEFINITION"; cardText.text = visible[index].definition; }
        counter.text = (index + 1) + " / " + visible.Count;
    }
}
