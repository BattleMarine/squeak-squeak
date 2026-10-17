using System;
using System.Linq;

namespace SqueakSqueak.Saving
{
    /// <summary>완성 결과만 보관합니다. caller는 성공 확인 전 메모리/화면을 확정하면 안 됩니다.</summary>
    public sealed class CompletedCoinStore
    {
        public const string FileName="completed-coins-v1.sqsave";
        private readonly CompletedCoinCodec codec;
        private readonly SafeSaveFileStore<CompletedCoinLibraryDto> store;
        public CompletedCoinStore(string directory,CoinSaveDefinitionCatalog definitions,SaveFileOperations files=null)
        {codec=new CompletedCoinCodec(definitions);store=new SafeSaveFileStore<CompletedCoinLibraryDto>(directory,FileName,codec,files);}
        public SaveReadResult<CompletedCoinLibraryDto> Load()=>store.Load();
        public SaveWriteResult Save(SavedCoinDto value)
        {
            SavedCoinDto candidate;
            try{codec.Validate(new CompletedCoinLibraryDto{formatVersion=1,results=new[]{value}});candidate=value.Copy();codec.Validate(new CompletedCoinLibraryDto{formatVersion=1,results=new[]{candidate}});}
            catch(SaveDataException e){return new SaveWriteResult(SaveWriteStatus.Rejected,e.Issue+": "+e.Message);}
            return store.Update(CompletedCoinLibraryDto.Empty,(current,_)=>
            {
                foreach(var saved in current.results)if(saved.resultId==candidate.resultId)
                    return UpdateDecision<CompletedCoinLibraryDto>.Return(codec.SameResult(saved,candidate)?SaveWriteStatus.AlreadySaved:SaveWriteStatus.Conflict,"Result ID already exists");
                if(current.results.Length>=CoinSaveLimits.MaximumResults)return UpdateDecision<CompletedCoinLibraryDto>.Return(SaveWriteStatus.Full,"Completed coin capacity reached; existing results preserved");
                return UpdateDecision<CompletedCoinLibraryDto>.Write(new CompletedCoinLibraryDto{formatVersion=1,results=current.results.Concat(new[]{candidate}).ToArray()});
            });
        }
    }
    /// <summary>결과 파일과 별도 책임/파일입니다. 보상·구매를 만들거나 실험 접근을 정식 소유로 기록하지 않습니다.</summary>
    public sealed class GeneralProgressStore
    {
        public const string FileName="general-progress-v1.sqsave";
        private readonly GeneralProgressCodec codec;
        private readonly SafeSaveFileStore<GeneralProgressDto> store;
        private readonly GeneralProgressSaveDefinition definitions;
        public GeneralProgressStore(string directory,GeneralProgressSaveDefinition definitions,SaveFileOperations files=null)
        {this.definitions=definitions;codec=new GeneralProgressCodec(definitions);store=new SafeSaveFileStore<GeneralProgressDto>(directory,FileName,codec,files);}
        public SaveReadResult<GeneralProgressDto> Load()=>store.Load();
        public SaveWriteResult Save(GeneralProgressDto value,long expectedRevision)
        {
            GeneralProgressDto copy;
            try{copy=codec.Decode(codec.Encode(value,1)).Data;}
            catch(SaveDataException e){return new SaveWriteResult(SaveWriteStatus.Rejected,e.Issue+": "+e.Message);}
            return store.Update(definitions.InitialProgress,(current,revision)=>revision!=expectedRevision?
                UpdateDecision<GeneralProgressDto>.Return(SaveWriteStatus.RevisionConflict,"Stale general progress revision"):
                UpdateDecision<GeneralProgressDto>.Write(copy));
        }
    }
}
