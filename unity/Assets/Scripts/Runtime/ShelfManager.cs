using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShelfManager : MonoBehaviour {

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float angleStepDegrees = 8f;
    [SerializeField] private int columnsPerRow = 6;
    [SerializeField] private int maxRows = 4;
    [SerializeField] private float rowVerticalSpacing = 0.25f;
    [SerializeField] private float cameraHeightOffset = 1.4f;

    private readonly List<AppCard> _cards = new List<AppCard>();
    private AppRepository _repo;

    void Start() {
        _repo = new AppRepository();
        _repo.Refresh();

        if (cardPrefab == null) {
            Debug.LogError("[QuestLauncher] ShelfManager: cardPrefab unassigned");
            return;
        }

        StartCoroutine(BuildShelf(_repo.All));
    }

    private IEnumerator BuildShelf(IReadOnlyList<AppInfo> apps) {
        Vector3 anchorPosition = transform.position;
        if (anchorPosition == Vector3.zero) {
            anchorPosition = new Vector3(0f, cameraHeightOffset, 0f);
            transform.position = anchorPosition;
        }

        int capacity = columnsPerRow * maxRows;
        int total = Mathf.Min(apps.Count, capacity);
        Debug.Log($"[QuestLauncher] Building shelf: {total} cards visible ({apps.Count} discovered, capacity {capacity}; pagination lands in Phase 5)");

        // Centre rows vertically around the anchor so the grid sits at eye level
        // instead of dropping into the floor.
        float rowOffsetCentre = (maxRows - 1) / 2f;

        for (int i = 0; i < total; i++) {
            int col = i % columnsPerRow;
            int row = i / columnsPerRow;

            float angle = (col - (columnsPerRow - 1) / 2f) * angleStepDegrees;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float verticalOffset = (rowOffsetCentre - row) * rowVerticalSpacing;
            Vector3 position = anchorPosition + dir * radius + Vector3.up * verticalOffset;

            var go = Instantiate(cardPrefab, position, Quaternion.identity, transform);
            go.transform.LookAt(anchorPosition);

            var card = go.GetComponent<AppCard>();
            if (card != null) {
                card.Bind(apps[i]);
                _cards.Add(card);
            }

            yield return null;
        }

        Debug.Log($"[QuestLauncher] Shelf ready: {_cards.Count} cards instantiated");
    }
}
