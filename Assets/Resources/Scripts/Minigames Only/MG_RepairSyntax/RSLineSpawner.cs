using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RSLineSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RSManager manager;
    [SerializeField] private GameObject codeLinePrefab;
    [SerializeField] private Transform spawnArea;

    private List<GameObject> spawnedLines = new List<GameObject>(); // Lists every code line currently in the world, so they can be despawned later


    

    public void DespawnCodeLines() // Destroys every code line this spawner made
    {
        for (int i = 0; i < spawnedLines.Count; i++)
        {
            if (spawnedLines[i] != null)
            {
                Destroy(spawnedLines[i]);
            }
        }

        spawnedLines.Clear();
    }

    public void SpawnCodeLines()
    {
        if (manager == null || codeLinePrefab == null || spawnArea == null)
        {
            Debug.LogError("RSLineSpawner: A reference is missing! (Are RSManager, CodeLine prefab, and SpawnArea assigned in the Inspector?)");
            return;
        }

        RepairSyntaxWave wave = manager.CurrentWave;

        if (wave == null || wave.codeLines == null)
        {
            Debug.LogError("RSLineSpawner: Current wave is missing!");
            return;
        }

        int count = wave.codeLines.Count;

        if (count == 0)
            return;

        DespawnCodeLines(); // Never stack a new set on top of an old one

        // Width of the area in world space.
        float areaWidth = spawnArea.lossyScale.x;

        // Half-width of a code line, so it stays inside the area.
        Renderer lineRenderer = codeLinePrefab.GetComponent<Renderer>();

        float lineHalfWidth = 0f;

        if (lineRenderer != null)
        {
            lineHalfWidth = lineRenderer.bounds.extents.x;
        }

        float usableWidth = Mathf.Max(0f, areaWidth - lineHalfWidth * 2f);

        float lineSpacing = 2f;
        float heightOffset = 1f;

        // Rotate the code line to lie flat against the SpawnArea.
        Quaternion lineRotation =
            spawnArea.rotation * Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < count; i++)
        {
            RepairSyntaxLineData lineData = wave.codeLines[i];

            if (lineData == null)
            {
                continue;
            }

            Vector3 position =
                spawnArea.position
                + spawnArea.up * heightOffset
                - spawnArea.forward * lineSpacing * i;

            GameObject instance = Instantiate(
                codeLinePrefab,
                position,
                lineRotation
            );

            instance.name = $"CodeLine_{i}";

            CodeLine codeLine = instance.GetComponent<CodeLine>();

            if (codeLine == null)
            {
                Debug.LogError($"{instance.name} has no CodeLine component! (Did you add it to the prefab?)");
                Destroy(instance);
                continue;
            }

            spawnedLines.Add(instance);

            codeLine.Initialize(lineData);

            TMP_Text text = instance.GetComponent<TMP_Text>();

            if (text == null)
            {
                Debug.LogError($"{instance.name} has no TextMeshPro component on its root! (Is the TMP text on the prefab's root object?)");
                continue;
            }

            if (lineData.hasSyntaxError)
            {
                text.text = lineData.brokenCode;
            }
            else
            {
                text.text = lineData.originalCode;
            }
        }
    }

}