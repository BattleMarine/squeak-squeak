using System;
using System.Collections.Generic;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Progression
{
    [Serializable]
    public struct CatalogTool
    {
        [SerializeField] private string id;
        [SerializeField] private CleaningToolDefinition definition;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private string unlockedByStageId;
        public string Id => id;
        public CleaningToolDefinition Definition => definition;
        public int Price => price;
        public string UnlockedByStageId => unlockedByStageId;
    }

    /// <summary>정적 콘텐츠 정의만 보유합니다. 재화 잔액·구매·완료 기록의 소유자가 아닙니다.</summary>
    [CreateAssetMenu(menuName = "Squeak Squeak/Progression/Catalog")]
    public sealed class ProgressionCatalog : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string currencyId = "cleaning-credit";
        [SerializeField] private string saveSlotId = "slot-01";
        [SerializeField, Min(0)] private int startingCurrency;
        [SerializeField] private Mesh objectDirtMesh;
        [SerializeField] private Material objectDirtMaterial;
        [SerializeField] private CoinDefinition[] coins = Array.Empty<CoinDefinition>();
        [SerializeField] private StageDefinition[] stages = Array.Empty<StageDefinition>();
        [SerializeField] private CatalogTool[] tools = Array.Empty<CatalogTool>();
        [SerializeField] private string[] initiallyOwnedToolIds = Array.Empty<string>();

        public string Id => id;
        public string CurrencyId => currencyId;
        public string SaveSlotId => saveSlotId;
        public int StartingCurrency => startingCurrency;
        public Mesh ObjectDirtMesh => objectDirtMesh;
        public Material ObjectDirtMaterial => objectDirtMaterial;
        public IReadOnlyList<CoinDefinition> Coins => coins;
        public IReadOnlyList<StageDefinition> Stages => stages;
        public IReadOnlyList<CatalogTool> Tools => tools;
        public IReadOnlyList<string> InitiallyOwnedToolIds => initiallyOwnedToolIds;
    }
}
