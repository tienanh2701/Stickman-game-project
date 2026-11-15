using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Quản lý màn hình Chọn Nhân Vật (Goku, Eren, Diend) & Chọn Màn (3 Thế giới x 4 Màn = 12 Màn).
    /// </summary>
    public class CharacterSelectManager : MonoBehaviour
    {
        private const string SELECTED_CHAR_KEY = "SelectedCharacter";
        private const string SELECTED_STAGE_KEY = "SelectedStageIndex";
        private const string UNLOCKED_STAGE_KEY = "Stickman_UnlockedStage";

        [Header("Character Cards (0=Goku, 1=Eren, 2=Diend)")]
        [SerializeField] private Button[] characterButtons;
        [SerializeField] private Image[] characterPortraits;
        [SerializeField] private Sprite[] normalPortraits;
        [SerializeField] private Sprite[] transformedPortraits;
        [SerializeField] private TextMeshProUGUI selectedCharacterText;
        [SerializeField] private TextMeshProUGUI characterDescriptionText;
        [SerializeField] private Button toggleFormButton;
        [SerializeField] private TextMeshProUGUI toggleFormButtonText;

        [Header("Stage Selection (12 Stages = 3 Worlds x 4 Stages)")]
        [SerializeField] private Button[] stageButtons;
        [SerializeField] private TextMeshProUGUI selectedStageText;

        [Header("Play & Debug")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button unlockAllButton;

        [Header("Target Scene")]
        [SerializeField] private string battleSceneName = "Stickman_DemoScene";

        private int selectedCharacter = 0;
        private int selectedStageIndex = 0;
        private bool showTransformedPortrait = false;

        private static readonly string[] CharacterNames =
        {
            "GOKU STICKMAN",
            "EREN STICKMAN",
            "DIEND STICKMAN"
        };

        private static readonly string[] WorldNames =
        {
            "W1: Attack on Titan",
            "W2: Dragon Ball",
            "W3: Kamen Rider Diend"
        };

        private static readonly string[] StageRoles =
        {
            "Stage 1 [Melee]",
            "Stage 2 [Ranged]",
            "Stage 3 [Elite]",
            "Stage 4 [BOSS]"
        };

        private void Start()
        {
            selectedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            selectedStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_STAGE_KEY, 0), 0, Mathf.Min(11, unlockedStage - 1));

            WireButtons();
            RefreshUI();
        }

        private void WireButtons()
        {
            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    int index = i;
                    if (characterButtons[i] != null)
                    {
                        characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
                    }
                }
            }

            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    int stageIdx = i;
                    if (stageButtons[i] != null)
                    {
                        stageButtons[i].onClick.AddListener(() => SelectStage(stageIdx));
                    }
                }
            }

            if (toggleFormButton != null)
            {
                toggleFormButton.onClick.AddListener(TogglePortraitForm);
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(PlaySelectedGame);
            }

            if (unlockAllButton != null)
            {
                unlockAllButton.onClick.AddListener(UnlockAllStages);
            }
        }

        public void SelectCharacter(int index)
        {
            selectedCharacter = Mathf.Clamp(index, 0, 2);
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharacter);
            PlayerPrefs.Save();
            RefreshUI();
        }

        public void SelectStage(int stageIndex)
        {
            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            if (stageIndex + 1 > unlockedStage) return;

            selectedStageIndex = Mathf.Clamp(stageIndex, 0, 11);
            PlayerPrefs.SetInt(SELECTED_STAGE_KEY, selectedStageIndex);
            PlayerPrefs.Save();
            RefreshUI();
        }

        public void TogglePortraitForm()
        {
            showTransformedPortrait = !showTransformedPortrait;
            RefreshUI();
        }

        public void UnlockAllStages()
        {
            PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, 12);
            PlayerPrefs.Save();
            RefreshUI();
        }

        public void PlaySelectedGame()
        {
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharacter);
            PlayerPrefs.SetInt(SELECTED_STAGE_KEY, selectedStageIndex);
            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(battleSceneName) && Application.CanStreamedLevelBeLoaded(battleSceneName))
            {
                SceneManager.LoadScene(battleSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                Debug.LogError("[CharacterSelectManager] Battle scene is not registered in Build Settings!");
            }
        }

        private void RefreshUI()
        {
            if (selectedCharacterText != null)
            {
                string formLabel = showTransformedPortrait ? " (TRANSFORMED PREVIEW)" : " (NORMAL FORM)";
                selectedCharacterText.text = $"NHAN VAT: {CharacterNames[selectedCharacter]}{formLabel}";
            }

            if (characterDescriptionText != null)
            {
                characterDescriptionText.text = string.Empty;
                characterDescriptionText.gameObject.SetActive(false);
            }

            if (toggleFormButtonText != null)
            {
                toggleFormButtonText.text = showTransformedPortrait ? "Xem Dang Thuong" : "Xem Dang Bien Hinh";
            }

            if (characterPortraits != null)
            {
                for (int i = 0; i < characterPortraits.Length; i++)
                {
                    if (characterPortraits[i] == null) continue;
                    Sprite chosen = showTransformedPortrait && transformedPortraits != null && i < transformedPortraits.Length
                        ? transformedPortraits[i]
                        : (normalPortraits != null && i < normalPortraits.Length ? normalPortraits[i] : null);
                    if (chosen != null) characterPortraits[i].sprite = chosen;
                    characterPortraits[i].preserveAspect = true;
                    characterPortraits[i].rectTransform.localScale = Vector3.one;
                }
            }

            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] == null) continue;
                    bool isSel = (i == selectedCharacter);
                    characterButtons[i].transform.localScale = isSel ? Vector3.one * 1.07f : Vector3.one;
                    Image bg = characterButtons[i].GetComponent<Image>();
                    if (bg != null)
                    {
                        bg.color = isSel ? new Color(0.95f, 0.72f, 0.15f, 1f) : new Color(0.22f, 0.24f, 0.30f, 1f);
                    }
                }
            }

            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    if (stageButtons[i] == null) continue;
                    bool isUnlocked = (i + 1) <= unlockedStage;
                    bool isSel = (i == selectedStageIndex);
                    stageButtons[i].interactable = isUnlocked;
                    Image bg = stageButtons[i].GetComponent<Image>();
                    if (bg != null)
                    {
                        if (!isUnlocked)
                            bg.color = new Color(0.18f, 0.18f, 0.18f, 0.7f);
                        else if (isSel)
                            bg.color = new Color(0.2f, 0.78f, 0.35f, 1f);
                        else
                            bg.color = new Color(0.26f, 0.34f, 0.48f, 1f);
                    }
                }
            }

            if (selectedStageText != null)
            {
                int worldIdx = selectedStageIndex / 4;
                int stageIdxInWorld = selectedStageIndex % 4;
                selectedStageText.text = $"MAN CHOI: {WorldNames[Mathf.Clamp(worldIdx, 0, 2)]} - {StageRoles[Mathf.Clamp(stageIdxInWorld, 0, 3)]}";
            }
        }
    }
}
