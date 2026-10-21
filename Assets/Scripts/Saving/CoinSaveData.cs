using System;
using System.Linq;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public static class CoinSaveLimits
    {
        public const int FormatVersion=1, DefinitionVersion=1, MaximumResults=16;
        public const int SideResultVersion=2;
        public const int MinimumResolution=16, MaximumResolution=256;
        public const int MaximumDecodedBytes=24*1024*1024, MaximumFileBytes=32*1024*1024;
        public static bool ResolutionValid(int value)=>value>=MinimumResolution&&value<=MaximumResolution&&(value&(value-1))==0;
    }

    // 저장 경계의 mutable DTO입니다. 씬 객체·실행 중 판/입력/경제 상태를 보유하지 않습니다.
    [Serializable] public sealed class SavedCoinDto
    {
        public int formatVersion,coinVersion,reactionVersion,layoutVersion;
        public string resultId,coinId,reactionId,layoutId,renderRevision,definitionFingerprint;
        public long roundId;
        public int resolution;
        public float elapsedSeconds;
        public double initialDirtSum;
        public float[] a,frontDirt,backDirt,frontPolish,backPolish;
        public bool[] frontSoftened,backSoftened;
        public float[] sideDirt=Array.Empty<float>(),sidePolish=Array.Empty<float>();
        public bool[] sideSoftened=Array.Empty<bool>();

        public static SavedCoinDto FromSnapshot(ExperimentSnapshot source,CoinSaveDefinitionCatalog definitions)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            var contract=definitions.Get(source.CoinId);
            var dto=new SavedCoinDto{formatVersion=source.HasSide?CoinSaveLimits.SideResultVersion:CoinSaveLimits.FormatVersion,coinVersion=contract.Version,reactionVersion=contract.Version,layoutVersion=contract.Version,
                resultId=source.ResultId,roundId=source.RoundId,coinId=source.CoinId,reactionId=source.ReactionId,layoutId=source.LayoutId,
                renderRevision=source.HasSide?contract.SideRenderRevision:contract.RenderRevision,definitionFingerprint=source.HasSide?contract.SideFingerprint:contract.Fingerprint,resolution=source.Resolution,elapsedSeconds=source.ElapsedSeconds,
                initialDirtSum=source.InitialDirtSum,a=source.A.ToArray(),frontDirt=source.FrontDirt.ToArray(),backDirt=source.BackDirt.ToArray(),
                frontPolish=source.FrontPolish.ToArray(),backPolish=source.BackPolish.ToArray(),frontSoftened=source.FrontSoftened.ToArray(),backSoftened=source.BackSoftened.ToArray(),
                sideDirt=source.SideDirt.ToArray(),sidePolish=source.SidePolish.ToArray(),sideSoftened=source.SideSoftened.ToArray()};
            new CoinSaveValidator(definitions).ValidateResult(dto);return dto;
        }
        public SavedCoinDto Copy()=>new(){formatVersion=formatVersion,coinVersion=coinVersion,reactionVersion=reactionVersion,layoutVersion=layoutVersion,
            resultId=resultId,coinId=coinId,reactionId=reactionId,layoutId=layoutId,renderRevision=renderRevision,definitionFingerprint=definitionFingerprint,
            roundId=roundId,resolution=resolution,elapsedSeconds=elapsedSeconds,initialDirtSum=initialDirtSum,
            a=(float[])a.Clone(),frontDirt=(float[])frontDirt.Clone(),backDirt=(float[])backDirt.Clone(),frontPolish=(float[])frontPolish.Clone(),backPolish=(float[])backPolish.Clone(),
            frontSoftened=(bool[])frontSoftened.Clone(),backSoftened=(bool[])backSoftened.Clone(),sideDirt=(float[])sideDirt.Clone(),sidePolish=(float[])sidePolish.Clone(),sideSoftened=(bool[])sideSoftened.Clone()};
    }
    [Serializable] public sealed class CompletedCoinLibraryDto
    {
        public int formatVersion;
        public SavedCoinDto[] results;
        public static CompletedCoinLibraryDto Empty()=>new(){formatVersion=CoinSaveLimits.FormatVersion,results=Array.Empty<SavedCoinDto>()};
    }
    [Serializable] public sealed class GeneralProgressDto
    {
        public int formatVersion;
        public string catalogId,selectedStageId;
        public string[] ownedToolIds,completedStageIds;
        // 실험 도구 목록·재화·상점·등급·진행 중 청소 상태는 이 형식에 없습니다.
    }
    public enum SaveIssue { None, Missing, Corrupt, Unsupported, UnknownDefinition, DefinitionChanged, IoError }
    public enum SaveSource { None, Primary, Backup }
    public enum SaveWriteStatus { Saved, AlreadySaved, Rejected, Conflict, Full, RevisionConflict, Failed, Indeterminate }
    public sealed class SaveDataException:Exception
    {
        public SaveIssue Issue {get;}
        public SaveDataException(SaveIssue issue,string message):base(message){Issue=issue;}
    }
    public sealed class SaveReadResult<T>
    {
        public SaveIssue Issue {get;}
        public SaveSource Source {get;}
        public string Message {get;}
        public T Data {get;}
        public long Revision {get;}
        public bool HasData=>Issue==SaveIssue.None;
        internal SaveReadResult(SaveIssue issue,SaveSource source,string message,T data=default,long revision=0)
        {Issue=issue;Source=source;Message=message;Data=data;Revision=revision;}
    }
    public sealed class SaveWriteResult
    {
        public SaveWriteStatus Status {get;}
        public string Message {get;}
        public long Revision {get;}
        public bool Succeeded=>Status==SaveWriteStatus.Saved||Status==SaveWriteStatus.AlreadySaved;
        internal SaveWriteResult(SaveWriteStatus status,string message,long revision=0){Status=status;Message=message;Revision=revision;}
    }
}
