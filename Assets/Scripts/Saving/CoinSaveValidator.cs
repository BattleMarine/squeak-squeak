using System;
using System.Collections.Generic;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    public sealed class CoinSaveValidator
    {
        private readonly CoinSaveDefinitionCatalog definitions;
        public CoinSaveValidator(CoinSaveDefinitionCatalog definitions){this.definitions=definitions??throw new ArgumentNullException(nameof(definitions));}
        internal static void Require(bool condition,string name){if(!condition)throw new SaveDataException(SaveIssue.Corrupt,name);}
        internal static void Version(int value){if(value!=CoinSaveLimits.FormatVersion)throw new SaveDataException(SaveIssue.Unsupported,"Unsupported save version: "+value);}
        public static bool ValidId(string id)
        {if(string.IsNullOrEmpty(id)||id.Length>96)return false;foreach(var c in id)if(!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_'||c=='.'))return false;return true;}
        public void ValidateLibrary(CompletedCoinLibraryDto library)
        {
            Require(library!=null,"Missing library");Version(library.formatVersion);
            Require(library.results!=null&&library.results.Length<=CoinSaveLimits.MaximumResults,"Invalid result count");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var result in library.results){ValidateResult(result);Require(ids.Add(result.resultId),"Duplicate result ID");}
        }
        public void ValidateResult(SavedCoinDto dto)
        {
            Require(dto!=null,"Missing result");Version(dto.formatVersion);
            Require(Guid.TryParseExact(dto.resultId,"N",out var id)&&id!=Guid.Empty&&dto.resultId==id.ToString("N"),"Invalid result GUID");
            Require(dto.roundId>0&&CoinSaveLimits.ResolutionValid(dto.resolution),"Invalid round/resolution");
            Require(ValidId(dto.coinId)&&ValidId(dto.reactionId)&&ValidId(dto.layoutId)&&ValidId(dto.renderRevision),"Invalid definition ID");
            var contract=definitions.Get(dto.coinId);
            if(dto.coinVersion!=contract.Version||dto.reactionVersion!=contract.Version||dto.layoutVersion!=contract.Version||dto.reactionId!=contract.ReactionId||dto.layoutId!=contract.LayoutId||dto.renderRevision!=contract.RenderRevision||dto.definitionFingerprint!=contract.Fingerprint)
                throw new SaveDataException(SaveIssue.DefinitionChanged,"Saved result definition/render contract no longer matches.");
            Require(float.IsFinite(dto.elapsedSeconds)&&dto.elapsedSeconds>=0,"Invalid elapsed time");
            Require(double.IsFinite(dto.initialDirtSum)&&dto.initialDirtSum>=0,"Invalid initial dirt sum");
            var n=dto.resolution*dto.resolution;
            Require(dto.a!=null&&dto.a.Length==contract.ACount,"Invalid A length");
            Require(dto.frontDirt?.Length==n&&dto.backDirt?.Length==n&&dto.frontPolish?.Length==n&&dto.backPolish?.Length==n&&dto.frontSoftened?.Length==n&&dto.backSoftened?.Length==n,"Invalid surface lengths");
            for(var i=0;i<dto.a.Length;i++)Require(float.IsFinite(dto.a[i])&&dto.a[i]==0,"Only fully removed A can be stored");
            var initial=contract.InitialMask(dto.resolution);
            Require(dto.initialDirtSum==initial.InitialDirtSum,"Initial dirt baseline mismatch");
            double current=0;
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})
            {
                var dirt=face==CoinSurfaceFace.Front?dto.frontDirt:dto.backDirt;var polish=face==CoinSurfaceFace.Front?dto.frontPolish:dto.backPolish;var softened=face==CoinSurfaceFace.Front?dto.frontSoftened:dto.backSoftened;
                for(var i=0;i<n;i++)
                {
                    Require(float.IsFinite(dirt[i])&&dirt[i]>=0&&dirt[i]<=initial.GetDirt(face,i),"Invalid/increased dirt");
                    Require(float.IsFinite(polish[i])&&polish[i]==0,"Rb format has no polish work");
                    Require(!softened[i]||dirt[i]>0&&initial.IsValidTexel(i),"Softened clean/outside texel");current+=dirt[i];
                }
            }
            Require(new ExperimentProgress(true,dto.a.Length,dto.a.Length,dto.initialDirtSum,current).IsComplete,"Unfinished cleaning cannot be stored");
        }
    }
}
