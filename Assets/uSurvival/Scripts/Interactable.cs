using UnityEngine;

public interface Interactable
{
    string GetInteractionText();
    void OnInteractClient(GameObject player);
    void OnInteractServer(GameObject player);
}
