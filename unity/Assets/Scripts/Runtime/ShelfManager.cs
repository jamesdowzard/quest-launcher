using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShelfManager : MonoBehaviour {

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private float angleStepDegrees = 15f;
    [SerializeField] private int columnsPerRow = 12;
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
        Debug.Log($"[QuestLauncher] Building shelf with {apps.Count} cards");

        Vector3 anchorPosition = transform.position;
        if (anchorPosition == Vector3.zero) {
            anchorPosition = new Vector3(0f, cameraHeightOffset, 0f);
            transform.position = anchorPosition;
        }

        for (int i = 0; i < apps.Count; i++) {
            int col = i % columnsPerRow;
            int row = i / columnsPerRow;

            float angle = (col - (columnsPerRow - 1) / 2f) * angleStepDegrees;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 position = anchorPosition + dir * radius + Vector3.down * (row * rowVerticalSpacing);

            var go = Instantiate(cardPrefab, position, Quaternion.identity, transform);
            go.transform.LookAt(anchorPosition);

            var card = go.GetComponent<AppCard>();
            if (card != null) {
                card.Bind(apps[i]);
                _cards.Add(card);
            }

            // Yield each card to amortise JNI getIcon cost across frames.
            yield return null;
        }

        Debug.Log($"[QuestLauncher] Shelf ready: {_cards.Count} cards instantiated");
    }
}
