using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UpgradeShopUI : MonoBehaviour
{
    enum UpgradeType
    {
        Health,
        Damage,
        Income
    }

    sealed class UpgradeCard
    {
        public UpgradeType Type;
        public Button Button;
        public TMP_Text LevelText;
        public TMP_Text PriceText;
        public Color PriceBaseColor;
        public int PurchasedLevels;
    }

    readonly List<UpgradeCard> cards = new List<UpgradeCard>();

    GameController gameController;
    Player2D player;
    int healthBaseCost;
    int damageBaseCost;
    int incomeBaseCost;
    float costGrowth;
    int healthPerLevel;
    int damagePerLevel;
    int incomePerLevel;

    public void Initialize(
        GameController controller,
        Player2D targetPlayer,
        int healthCost,
        int damageCost,
        int incomeCost,
        float priceGrowth,
        int healthIncrease,
        int damageIncrease,
        int incomeIncrease)
    {
        Unbind();
        gameController = controller;
        player = targetPlayer;
        healthBaseCost = Mathf.Max(1, healthCost);
        damageBaseCost = Mathf.Max(1, damageCost);
        incomeBaseCost = Mathf.Max(1, incomeCost);
        costGrowth = Mathf.Max(1f, priceGrowth);
        healthPerLevel = Mathf.Max(1, healthIncrease);
        damagePerLevel = Mathf.Max(1, damageIncrease);
        incomePerLevel = Mathf.Max(1, incomeIncrease);

        BuildCards();

        if (gameController != null)
            gameController.CoinsChanged += HandleCoinsChanged;
        if (player != null)
        {
            player.StatsChanged += RefreshCards;
            player.HealthChanged += HandleHealthChanged;
        }

        RefreshCards();
    }

    void BuildCards()
    {
        cards.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform cardRoot = transform.GetChild(i);
            TMP_Text titleText = FindDirectChildText(cardRoot, "TextElement");
            if (titleText == null || !TryResolveType(titleText.text, out UpgradeType type))
                continue;

            Transform buttonRoot = FindDescendant(cardRoot, "btn");
            if (buttonRoot == null)
                continue;

            Image buttonImage = buttonRoot.GetComponent<Image>();
            Button button = buttonRoot.GetComponent<Button>();
            if (button == null)
                button = buttonRoot.gameObject.AddComponent<Button>();

            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.RemoveAllListeners();

            TMP_Text priceText = buttonRoot.GetComponentInChildren<TMP_Text>(true);
            var card = new UpgradeCard
            {
                Type = type,
                Button = button,
                LevelText = FindDirectChildText(cardRoot, "LevelElement"),
                PriceText = priceText,
                PriceBaseColor = priceText != null ? priceText.color : Color.white,
                PurchasedLevels = 0
            };

            button.onClick.AddListener(() => TryPurchase(card));
            cards.Add(card);
        }
    }

    void TryPurchase(UpgradeCard card)
    {
        if (gameController == null || player == null || !player.IsAlive)
            return;

        int cost = GetCost(card);
        if (!gameController.TrySpendCoins(cost))
        {
            SoundManager.Instance?.PlayUpgradeError();
            if (card.Button != null)
            {
                RectTransform buttonTransform = card.Button.transform as RectTransform;
                if (buttonTransform != null)
                {
                    buttonTransform.DOKill();
                    buttonTransform.DOShakeAnchorPos(0.22f, 8f, 18, 75f)
                        .SetUpdate(true)
                        .SetTarget(buttonTransform);
                }
            }
            return;
        }

        SoundManager.Instance?.PlayUpgradePurchased();

        switch (card.Type)
        {
            case UpgradeType.Health:
                player.UpgradeMaxHealth(healthPerLevel, true);
                break;
            case UpgradeType.Damage:
                player.UpgradeDamage(damagePerLevel);
                break;
            case UpgradeType.Income:
                player.UpgradeIncome(incomePerLevel);
                break;
        }

        card.PurchasedLevels++;
        RefreshCards();
    }

    int GetCost(UpgradeCard card)
    {
        int baseCost = card.Type switch
        {
            UpgradeType.Health => healthBaseCost,
            UpgradeType.Damage => damageBaseCost,
            _ => incomeBaseCost
        };

        return Mathf.CeilToInt(baseCost * Mathf.Pow(costGrowth, card.PurchasedLevels));
    }

    void HandleCoinsChanged(int _) => RefreshCards();

    void HandleHealthChanged(int _, int __) => RefreshCards();

    void RefreshCards()
    {
        if (gameController == null || player == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            UpgradeCard card = cards[i];
            int cost = GetCost(card);

            if (card.PriceText != null)
            {
                card.PriceText.text = cost.ToString();
                card.PriceText.color = gameController.Coins >= cost
                    ? card.PriceBaseColor
                    : new Color(1f, 0.28f, 0.2f, 1f);
            }
            if (card.LevelText != null)
                card.LevelText.text = $"Level {card.PurchasedLevels + 1}";
            if (card.Button != null)
                card.Button.interactable = player.IsAlive;
        }
    }

    static bool TryResolveType(string title, out UpgradeType type)
    {
        string normalized = title.Trim().ToLowerInvariant();
        if (normalized.Contains("health") || normalized.Contains("vida"))
        {
            type = UpgradeType.Health;
            return true;
        }
        if (normalized.Contains("damage") || normalized.Contains("daño") || normalized.Contains("dano"))
        {
            type = UpgradeType.Damage;
            return true;
        }
        if (normalized.Contains("income") || normalized.Contains("ingreso"))
        {
            type = UpgradeType.Income;
            return true;
        }

        type = default;
        return false;
    }

    static TMP_Text FindDirectChildText(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child.GetComponent<TMP_Text>();
        }
        return null;
    }

    static Transform FindDescendant(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), childName);
            if (found != null)
                return found;
        }
        return null;
    }

    void Unbind()
    {
        if (gameController != null)
            gameController.CoinsChanged -= HandleCoinsChanged;
        if (player != null)
        {
            player.StatsChanged -= RefreshCards;
            player.HealthChanged -= HandleHealthChanged;
        }
    }

    void OnDestroy() => Unbind();
}
