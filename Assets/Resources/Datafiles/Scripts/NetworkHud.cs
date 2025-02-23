using FishNet;
using UnityEngine;
using UnityEngine.UI;

public class NetworkHud : MonoBehaviour
{
    [Header("Network Controls")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button startServerButton;

    private void Start()
    {
        SetupNetworkControls();
    }

    private void SetupNetworkControls()
    {
        startHostButton?.onClick.AddListener(() => {
            Debug.Log("Starting Host");
            InstanceFinder.NetworkManager.ServerManager.StartConnection();
            InstanceFinder.NetworkManager.ClientManager.StartConnection();
        });

        startClientButton?.onClick.AddListener(() => {
            Debug.Log("Starting Client");
            //InstanceFinder.NetworkManager.ClientManager.StartConnection();
            InstanceFinder.NetworkManager.ClientManager.StartConnection("127.0.0.1");
        });

        startServerButton?.onClick.AddListener(() => {
            Debug.Log("Starting Server");
            InstanceFinder.NetworkManager.ServerManager.StartConnection();
        });
    }
}
