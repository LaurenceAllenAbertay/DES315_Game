using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathScreen : MonoBehaviour
{
    [SerializeField] private Player player;

    private Animator animator;
    void Start()
    {
        player = FindFirstObjectByType<Player>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (player.CurrentHealth == 0)
            animator.SetTrigger("Death");
    }

    public void MoveToMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
