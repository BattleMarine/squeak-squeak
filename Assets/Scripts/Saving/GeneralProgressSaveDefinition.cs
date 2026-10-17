using System;
using System.Collections.Generic;
using System.Linq;
using SqueakSqueak.Progression;

namespace SqueakSqueak.Saving
{
    /// <summary>메인 스레드에서 정적 정의를 동결합니다. 저장 worker는 Unity 객체를 참조하지 않습니다.</summary>
    public sealed class GeneralProgressSaveDefinition
    {
        private sealed class StageRule
        {
            internal readonly string[] Prerequisites,RequiredTools,AllowedTools;
            internal StageRule(StageDefinition stage){Prerequisites=stage.PrerequisiteStageIds.ToArray();RequiredTools=stage.RequiredOwnedToolIds.ToArray();AllowedTools=stage.AllowedToolIds.ToArray();}
        }
        private readonly string catalogId,initialStage;
        private readonly string[] initialTools;
        private readonly Dictionary<string,string> toolUnlocks=new(StringComparer.Ordinal);
        private readonly Dictionary<string,StageRule> stages=new(StringComparer.Ordinal);
        public int ToolCount=>toolUnlocks.Count;
        public int StageCount=>stages.Count;
        public GeneralProgressSaveDefinition(ProgressionCatalog catalog)
        {
            if(catalog==null||ProgressionCatalogValidator.Validate(catalog).Count!=0)throw new ArgumentException("Invalid progression definitions");
            catalogId=catalog.Id;initialStage=catalog.Stages[0].Id;initialTools=catalog.InitiallyOwnedToolIds.ToArray();
            foreach(var tool in catalog.Tools)toolUnlocks.Add(tool.Id,tool.UnlockedByStageId);
            foreach(var stage in catalog.Stages)stages.Add(stage.Id,new StageRule(stage));
        }
        public GeneralProgressDto InitialProgress()=>new(){formatVersion=1,catalogId=catalogId,selectedStageId=initialStage,ownedToolIds=(string[])initialTools.Clone(),completedStageIds=Array.Empty<string>()};
        public void Validate(GeneralProgressDto dto)
        {
            CoinSaveValidator.Require(dto!=null,"Missing general progress");CoinSaveValidator.Version(dto.formatVersion);
            if(dto.catalogId!=catalogId)throw new SaveDataException(SaveIssue.UnknownDefinition,"Unknown progress catalog");
            CoinSaveValidator.Require(dto.ownedToolIds!=null&&dto.completedStageIds!=null&&dto.ownedToolIds.Length<=ToolCount&&dto.completedStageIds.Length<=StageCount,"Invalid progress arrays");
            var owned=new HashSet<string>(StringComparer.Ordinal);var completed=new HashSet<string>(StringComparer.Ordinal);
            foreach(var id in dto.ownedToolIds)
            {CoinSaveValidator.Require(CoinSaveValidator.ValidId(id)&&owned.Add(id),"Invalid/duplicate owned ID");if(!toolUnlocks.ContainsKey(id))throw new SaveDataException(SaveIssue.UnknownDefinition,"Unknown owned tool");}
            foreach(var id in dto.completedStageIds)
            {CoinSaveValidator.Require(CoinSaveValidator.ValidId(id)&&completed.Add(id),"Invalid/duplicate completed ID");if(!stages.ContainsKey(id))throw new SaveDataException(SaveIssue.UnknownDefinition,"Unknown completed stage");}
            foreach(var id in owned)CoinSaveValidator.Require(string.IsNullOrEmpty(toolUnlocks[id])||completed.Contains(toolUnlocks[id]),"Ownership prerequisite missing");
            foreach(var id in initialTools)CoinSaveValidator.Require(owned.Contains(id),"Initial ownership missing");
            bool CanEnter(StageRule stage)=>stage.Prerequisites.All(completed.Contains)&&stage.RequiredTools.All(owned.Contains)&&stage.AllowedTools.Any(owned.Contains);
            foreach(var id in completed)CoinSaveValidator.Require(CanEnter(stages[id]),"Completed stage prerequisites missing");
            if(dto.selectedStageId==null||!stages.TryGetValue(dto.selectedStageId,out var selected))throw new SaveDataException(SaveIssue.UnknownDefinition,"Unknown selected stage");
            CoinSaveValidator.Require(CanEnter(selected),"Selected stage prerequisites missing");
        }
    }
}
