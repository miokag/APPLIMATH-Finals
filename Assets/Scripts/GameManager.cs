using UnityEngine;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Player stats
    [Header("Player Settings")]
    public int maxHealth = 3;
    public float invincibilityDuration = 5f;
    private int currentHealth;
    private bool isInvincible = false;

    // UI Elements
    [Header("UI Elements")]
    public TMP_Text healthText;
    public TMP_Text timerText;
    public GameObject gameOverPanel;

    // Game state
    [Header("Game Settings")]
    public float gameDuration = 180f; // 3 minutes
    private float currentTime;
    private bool gameRunning = true;

    // Power-up references
    private PowerUpManager powerUpManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        powerUpManager = FindObjectOfType<PowerUpManager>();
        InitializeGame();
    }

    void InitializeGame()
    {
        currentHealth = maxHealth;
        currentTime = gameDuration;
        UpdateHealthUI();
        UpdateTimerUI();
        gameOverPanel.SetActive(false);
        gameRunning = true;
        StartCoroutine(GameTimer());
    }

    IEnumerator GameTimer()
    {
        while (gameRunning && currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            UpdateTimerUI();
            
            if (currentTime <= 0)
            {
                GameOver("Time's up!");
            }
            
            yield return null;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;

        currentHealth -= damage;
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            GameOver("You died!");
        }
    }

    public void HealPlayer(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    public void ActivateInvincibility()
    {
        if (!isInvincible)
        {
            StartCoroutine(InvincibilityRoutine());
        }
    }

    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = $"HP: {currentHealth}/{maxHealth}";
            
            // Visual feedback when invincible
            healthText.color = isInvincible ? Color.yellow : Color.white;
        }
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    void GameOver(string reason)
    {
        gameRunning = false;
        gameOverPanel.SetActive(true);
        
        // Update game over text if needed
        TMP_Text gameOverText = gameOverPanel.GetComponentInChildren<TMP_Text>();
        if (gameOverText != null)
        {
            gameOverText.text = $"Game Over\n{reason}";
        }
        
        Time.timeScale = 0f; // Pause game
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public bool IsPlayerInvincible()
    {
        return isInvincible;
    }
}