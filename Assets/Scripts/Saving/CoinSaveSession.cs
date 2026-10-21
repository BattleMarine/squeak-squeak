using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SqueakSqueak.Experiment;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Saving
{
    public sealed class CoinSaveLoad
    {
        public bool Succeeded {get;internal set;}
        public string Message {get;internal set;}
        public ExperimentSnapshot[] Results {get;internal set;}
        public GeneralProgressDto Progress {get;internal set;}
        public long ProgressRevision {get;internal set;}
    }
    public sealed class CoinSaveCommit
    {
        public SaveWriteResult Write {get;internal set;}
        public ExperimentSnapshot[] Results {get;internal set;}
    }

    /// <summary>값 계약만 소유하는 세션. Unity 객체/씬/UI를 worker에서 조회하지 않습니다.</summary>
    public sealed class CoinSaveSession
    {
        public const string RenderRevision="cleaning-room-rb-v1";
        private readonly CompletedCoinStore coins;
        private readonly GeneralProgressStore progress;
        private readonly GeneralProgressSaveDefinition progressDefinitions;
        public CoinSaveDefinitionCatalog Definitions {get;}
        public string DirectoryPath {get;}
        public CoinSaveSession(string directory,CoinSaveDefinitionCatalog definitions,GeneralProgressSaveDefinition general,SaveFileOperations files=null)
        {
            DirectoryPath=Path.GetFullPath(directory);Definitions=definitions;progressDefinitions=general;
            coins=new CompletedCoinStore(directory,definitions,files);progress=new GeneralProgressStore(directory,general,files);
        }
        public CoinSaveLoad Load()
        {
            try
            {
                var c=coins.Load();var p=progress.Load();
                if(!c.HasData&&c.Issue!=SaveIssue.Missing)return new(){Message="보관함: "+c.Issue+" / "+c.Message};
                if(!p.HasData&&p.Issue!=SaveIssue.Missing)return new(){Message="일반 진행: "+p.Issue+" / "+p.Message};
                // 양쪽 읽기가 안전한 경우에만 최초 일반 진행을 만듭니다. 오류를 초기화로 숨기지 않습니다.
                if(p.Issue==SaveIssue.Missing)
                {
                    var initial=progressDefinitions.InitialProgress();var saved=progress.Save(initial,0);
                    if(!saved.Succeeded)return new(){Message="최초 진행 저장 실패: "+saved.Message};
                    p=progress.Load();if(!p.HasData)return new(){Message="최초 진행 재조회 실패: "+p.Message};
                }
                return new(){Succeeded=true,Results=Snapshots(c.HasData?c.Data:CompletedCoinLibraryDto.Empty()),Progress=p.Data,ProgressRevision=p.Revision,
                    Message=c.Source==SaveSource.Backup||p.Source==SaveSource.Backup?"손상된 본 파일 대신 검증된 백업을 읽었습니다. 원본은 보존됩니다.":null};
            }
            catch(Exception e){return new(){Message="저장 읽기 실패: "+e.Message};}
        }
        public CoinSaveCommit Store(SavedCoinDto candidate)
        {
            try
            {
                var result=coins.Save(candidate);
                if(!result.Succeeded)return new(){Write=result};
                var read=coins.Load();
                var found=read.HasData?read.Data.results.FirstOrDefault(x=>x.resultId==candidate.resultId):null;
                if(found==null||!new CompletedCoinCodec(Definitions).SameResult(found,candidate))
                    return new(){Write=new SaveWriteResult(SaveWriteStatus.Indeterminate,"쓰기 뒤 동일 결과 재조회 실패. 같은 ID로 재시도하세요.")};
                return new(){Write=result,Results=Snapshots(read.Data)};
            }
            catch(Exception e){return new(){Write=new SaveWriteResult(SaveWriteStatus.Indeterminate,e.Message)};}
        }
        public SaveWriteResult StoreProgress(GeneralProgressDto candidate,long expectedRevision)
        {
            try
            {
                var read=progress.Load();
                if(read.HasData&&SameProgress(read.Data,candidate))return new(SaveWriteStatus.AlreadySaved,"Same progress confirmed",read.Revision);
                var saved=progress.Save(candidate,expectedRevision);if(!saved.Succeeded)return saved;
                read=progress.Load();return read.HasData&&SameProgress(read.Data,candidate)?
                    new(SaveWriteStatus.Saved,"Progress confirmed",read.Revision):new(SaveWriteStatus.Indeterminate,"일반 진행 재조회 실패. 재시도하세요.");
            }
            catch(Exception e){return new(SaveWriteStatus.Indeterminate,e.Message);}
        }
        private static bool SameProgress(GeneralProgressDto a,GeneralProgressDto b)=>a.catalogId==b.catalogId&&a.selectedStageId==b.selectedStageId&&
            a.ownedToolIds.SequenceEqual(b.ownedToolIds)&&a.completedStageIds.SequenceEqual(b.completedStageIds);
        private static ExperimentSnapshot[] Snapshots(CompletedCoinLibraryDto library)=>library.results.Select(d=>ExperimentSnapshot.Restore(d.resultId,d.roundId,d.coinId,d.reactionId,d.layoutId,
            d.resolution,d.elapsedSeconds,d.initialDirtSum,d.a,d.frontDirt,d.backDirt,d.frontPolish,d.backPolish,d.frontSoftened,d.backSoftened,d.sideDirt,d.sidePolish,d.sideSoftened)).ToArray();
        public CoinSurfaceMaskData RestoreMask(ExperimentSnapshot value)
        {
            var mask=Definitions.Get(value.CoinId).InitialMask(value.Resolution,value.HasSide);
            for(var i=0;i<mask.TexelCount;i++)
            {
                mask.TrySetDirt(CoinSurfaceFace.Front,i,value.FrontDirt[i]);mask.TrySetDirt(CoinSurfaceFace.Back,i,value.BackDirt[i]);
                mask.TrySetPolish(CoinSurfaceFace.Front,i,value.FrontPolish[i]);mask.TrySetPolish(CoinSurfaceFace.Back,i,value.BackPolish[i]);
                if(value.FrontSoftened[i])mask.TrySoften(CoinSurfaceFace.Front,i);if(value.BackSoftened[i])mask.TrySoften(CoinSurfaceFace.Back,i);
            }
            for(var i=0;i<mask.SideTexelCount;i++)
            {
                mask.TrySetDirt(CoinSurfaceFace.Side,i,value.SideDirt[i]);mask.TrySetPolish(CoinSurfaceFace.Side,i,value.SidePolish[i]);if(value.SideSoftened[i])mask.TrySoften(CoinSurfaceFace.Side,i);
            }
            return mask;
        }
    }
}
