using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SqueakSqueak.Saving
{
    public sealed class DecodedSave<T>
    {
        public T Data {get;} public long Revision {get;}
        internal DecodedSave(T data,long revision){Data=data;Revision=revision;}
    }
    public abstract class SaveCodec<T>
    {
        private static readonly byte[] Magic=Encoding.ASCII.GetBytes("SQCOIN01");
        private readonly int kind;
        protected SaveCodec(int kind){this.kind=kind;}
        public abstract void Validate(T value);
        protected abstract void Write(BinaryWriter writer,T value);
        protected abstract T Read(BinaryReader reader);
        public byte[] Encode(T value,long revision)
        {
            Validate(value);CoinSaveValidator.Require(revision>0,"Invalid save revision");
            using var raw=new MemoryStream();using(var writer=new BinaryWriter(raw,Encoding.UTF8,true)){writer.Write(revision);Write(writer,value);}
            CoinSaveValidator.Require(raw.Length<=CoinSaveLimits.MaximumDecodedBytes,"Decoded size limit");
            var payload=raw.ToArray();byte[] compressed;
            using(var packed=new MemoryStream()){using(var gzip=new GZipStream(packed,CompressionLevel.Optimal,true))gzip.Write(payload,0,payload.Length);compressed=packed.ToArray();}
            using var output=new MemoryStream();using var header=new BinaryWriter(output);
            using var sha=SHA256.Create();header.Write(Magic);header.Write(CoinSaveLimits.FormatVersion);header.Write(kind);header.Write(payload.Length);header.Write(compressed.Length);header.Write(sha.ComputeHash(payload));header.Write(compressed);header.Flush();
            CoinSaveValidator.Require(output.Length<=CoinSaveLimits.MaximumFileBytes,"File size limit");return output.ToArray();
        }
        public DecodedSave<T> Decode(byte[] file)
        {
            try
            {
                CoinSaveValidator.Require(file!=null&&file.Length>=12,"Invalid file size");
                if(file.Length>CoinSaveLimits.MaximumFileBytes)throw new SaveDataException(SaveIssue.Unsupported,"Save file exceeds supported limit");
                using var input=new MemoryStream(file,false);using var reader=new BinaryReader(input);
                CoinSaveValidator.Require(reader.ReadBytes(8).SequenceEqual(Magic),"Invalid magic");CoinSaveValidator.Version(reader.ReadInt32());
                CoinSaveValidator.Require(file.Length>=56,"Truncated current envelope");
                if(reader.ReadInt32()!=kind)throw new SaveDataException(SaveIssue.Unsupported,"Different save document kind");
                var rawLength=reader.ReadInt32();var packedLength=reader.ReadInt32();var digest=reader.ReadBytes(32);
                CoinSaveValidator.Require(rawLength>=8&&rawLength<=CoinSaveLimits.MaximumDecodedBytes&&packedLength>0&&packedLength==input.Length-input.Position,"Invalid payload size");
                var payload=new byte[rawLength];
                using(var gzip=new GZipStream(input,CompressionMode.Decompress,true))
                {var read=0;while(read<payload.Length){var n=gzip.Read(payload,read,payload.Length-read);if(n==0)break;read+=n;}CoinSaveValidator.Require(read==payload.Length&&gzip.ReadByte()==-1,"Truncated/oversized decoded payload");}
                using var sha=SHA256.Create();CoinSaveValidator.Require(sha.ComputeHash(payload).SequenceEqual(digest),"Payload digest mismatch");
                using var raw=new MemoryStream(payload,false);using var body=new BinaryReader(raw);var revision=body.ReadInt64();CoinSaveValidator.Require(revision>0,"Invalid revision");
                var dto=Read(body);CoinSaveValidator.Require(raw.Position==raw.Length,"Unexpected trailing data");Validate(dto);return new DecodedSave<T>(dto,revision);
            }
            catch(SaveDataException){throw;}
            catch(Exception e)when(e is IOException||e is ArgumentException||e is OverflowException)
            {throw new SaveDataException(SaveIssue.Corrupt,"Malformed save: "+e.Message);}
        }
        internal static void Text(BinaryWriter writer,string value)
        {var bytes=new UTF8Encoding(false,true).GetBytes(value??throw new SaveDataException(SaveIssue.Corrupt,"Missing string"));CoinSaveValidator.Require(bytes.Length<=384,"String limit");writer.Write(bytes.Length);writer.Write(bytes);}
        internal static string Text(BinaryReader reader)
        {var n=reader.ReadInt32();CoinSaveValidator.Require(n>=0&&n<=384&&n<=reader.BaseStream.Length-reader.BaseStream.Position,"Invalid string length");return new UTF8Encoding(false,true).GetString(reader.ReadBytes(n));}
    }
    public sealed class CompletedCoinCodec:SaveCodec<CompletedCoinLibraryDto>
    {
        private readonly CoinSaveValidator validator;
        public CompletedCoinCodec(CoinSaveDefinitionCatalog definitions):base(1){validator=new CoinSaveValidator(definitions);}
        public override void Validate(CompletedCoinLibraryDto value)=>validator.ValidateLibrary(value);
        protected override void Write(BinaryWriter writer,CompletedCoinLibraryDto value)
        {writer.Write(value.formatVersion);writer.Write(value.results.Length);foreach(var r in value.results)WriteResult(writer,r);}
        internal static void WriteResult(BinaryWriter w,SavedCoinDto r)
        {
            w.Write(r.formatVersion);w.Write(r.coinVersion);w.Write(r.reactionVersion);w.Write(r.layoutVersion);
            Text(w,r.resultId);Text(w,r.coinId);Text(w,r.reactionId);Text(w,r.layoutId);Text(w,r.renderRevision);Text(w,r.definitionFingerprint);
            w.Write(r.roundId);w.Write(r.resolution);w.Write(r.elapsedSeconds);w.Write(r.initialDirtSum);w.Write(r.a.Length);
            foreach(var a in r.a)w.Write(a);foreach(var a in new[]{r.frontDirt,r.backDirt,r.frontPolish,r.backPolish})foreach(var v in a)w.Write(v);
            foreach(var array in new[]{r.frontSoftened,r.backSoftened})for(var i=0;i<array.Length;i+=8){byte b=0;for(var bit=0;bit<8&&i+bit<array.Length;bit++)if(array[i+bit])b|=(byte)(1<<bit);w.Write(b);}
            if(r.formatVersion==CoinSaveLimits.SideResultVersion)
            {
                foreach(var a in new[]{r.sideDirt,r.sidePolish})foreach(var v in a)w.Write(v);
                for(var i=0;i<r.sideSoftened.Length;i+=8){byte b=0;for(var bit=0;bit<8&&i+bit<r.sideSoftened.Length;bit++)if(r.sideSoftened[i+bit])b|=(byte)(1<<bit);w.Write(b);}
            }
        }
        public bool SameResult(SavedCoinDto a,SavedCoinDto b)
        {using var left=new MemoryStream();using var right=new MemoryStream();using(var w=new BinaryWriter(left,System.Text.Encoding.UTF8,true))WriteResult(w,a);using(var w=new BinaryWriter(right,System.Text.Encoding.UTF8,true))WriteResult(w,b);return left.ToArray().SequenceEqual(right.ToArray());}
        protected override CompletedCoinLibraryDto Read(BinaryReader r)
        {
            var dto=new CompletedCoinLibraryDto{formatVersion=r.ReadInt32()};CoinSaveValidator.Version(dto.formatVersion);var count=r.ReadInt32();
            CoinSaveValidator.Require(count>=0&&count<=CoinSaveLimits.MaximumResults,"Invalid result count");dto.results=new SavedCoinDto[count];
            for(var i=0;i<count;i++)
            {
                var v=new SavedCoinDto{formatVersion=r.ReadInt32(),coinVersion=r.ReadInt32(),reactionVersion=r.ReadInt32(),layoutVersion=r.ReadInt32()};CoinSaveValidator.ResultVersion(v.formatVersion);
                v.resultId=Text(r);v.coinId=Text(r);v.reactionId=Text(r);v.layoutId=Text(r);v.renderRevision=Text(r);v.definitionFingerprint=Text(r);
                v.roundId=r.ReadInt64();v.resolution=r.ReadInt32();v.elapsedSeconds=r.ReadSingle();v.initialDirtSum=r.ReadDouble();var aCount=r.ReadInt32();
                CoinSaveValidator.Require(CoinSaveLimits.ResolutionValid(v.resolution)&&aCount>=0&&aCount<=64,"Invalid resolution/A count");var n=v.resolution*v.resolution;
                var expected=(long)aCount*4+(long)n*16+2*((n+7)/8);CoinSaveValidator.Require(expected<=r.BaseStream.Length-r.BaseStream.Position,"Truncated arrays");
                v.a=new float[aCount];for(var j=0;j<aCount;j++)v.a[j]=r.ReadSingle();
                var arrays=new float[4][];for(var channel=0;channel<4;channel++){arrays[channel]=new float[n];for(var j=0;j<n;j++)arrays[channel][j]=r.ReadSingle();}
                v.frontDirt=arrays[0];v.backDirt=arrays[1];v.frontPolish=arrays[2];v.backPolish=arrays[3];
                var soft=new bool[2][];for(var face=0;face<2;face++){soft[face]=new bool[n];for(var j=0;j<n;j+=8){var b=r.ReadByte();for(var bit=0;bit<8&&j+bit<n;bit++)soft[face][j+bit]=(b&(1<<bit))!=0;}}
                v.frontSoftened=soft[0];v.backSoftened=soft[1];dto.results[i]=v;
                if(v.formatVersion==CoinSaveLimits.SideResultVersion)
                {
                    var sideN=v.resolution*Math.Max(4,v.resolution/16);
                    CoinSaveValidator.Require((long)sideN*8+(sideN+7)/8<=r.BaseStream.Length-r.BaseStream.Position,"Truncated side arrays");
                    v.sideDirt=new float[sideN];v.sidePolish=new float[sideN];v.sideSoftened=new bool[sideN];
                    foreach(var array in new[]{v.sideDirt,v.sidePolish})for(var j=0;j<sideN;j++)array[j]=r.ReadSingle();
                    for(var j=0;j<sideN;j+=8){var b=r.ReadByte();for(var bit=0;bit<8&&j+bit<sideN;bit++)v.sideSoftened[j+bit]=(b&(1<<bit))!=0;}
                }
            }
            return dto;
        }
    }
    public sealed class GeneralProgressCodec:SaveCodec<GeneralProgressDto>
    {
        private readonly GeneralProgressSaveDefinition definitions;
        public GeneralProgressCodec(GeneralProgressSaveDefinition definitions):base(2)
        {this.definitions=definitions??throw new ArgumentNullException(nameof(definitions));}
        public override void Validate(GeneralProgressDto value)=>definitions.Validate(value);
        protected override void Write(BinaryWriter w,GeneralProgressDto v)
        {w.Write(v.formatVersion);Text(w,v.catalogId);Text(w,v.selectedStageId);foreach(var a in new[]{v.ownedToolIds,v.completedStageIds}){w.Write(a.Length);foreach(var id in a)Text(w,id);}}
        protected override GeneralProgressDto Read(BinaryReader r)
        {
            var v=new GeneralProgressDto{formatVersion=r.ReadInt32()};CoinSaveValidator.Version(v.formatVersion);v.catalogId=Text(r);v.selectedStageId=Text(r);
            var arrays=new string[2][];for(var k=0;k<2;k++){var n=r.ReadInt32();CoinSaveValidator.Require(n>=0&&n<=(k==0?definitions.ToolCount:definitions.StageCount),"Invalid progress count");arrays[k]=new string[n];for(var i=0;i<n;i++)arrays[k][i]=Text(r);}
            v.ownedToolIds=arrays[0];v.completedStageIds=arrays[1];return v;
        }
    }
}
