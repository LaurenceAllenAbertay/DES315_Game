using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MessageUI : MonoBehaviour
{
    public static MessageUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Timing")]
    [SerializeField] private float messageDuration;

    private readonly Queue<string> messageQueue = new Queue<string>();
    private Coroutine displayCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (messageText != null)
        {
            messageText.text = string.Empty;
            messageText.enabled = false;
        }
    }

    public void EnqueueMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        messageQueue.Enqueue(message);

        if (displayCoroutine == null)
        {
            displayCoroutine = StartCoroutine(DisplayMessages());
        }
    }

    private IEnumerator DisplayMessages()
    {
        while (messageQueue.Count > 0)
        {
            string nextMessage = messageQueue.Dequeue();

            if (messageText != null)
            {
                messageText.text = nextMessage;
                messageText.enabled = true;
            }

            float duration = Mathf.Max(0.1f, messageDuration);
            yield return new WaitForSeconds(duration);
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
            messageText.enabled = false;
        }

        displayCoroutine = null;
    }

    private void OnDisable()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        messageQueue.Clear();

        if (messageText != null)
        {
            messageText.text = string.Empty;
            messageText.enabled = false;
        }
    }
}
