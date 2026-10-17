using System;
using System.IO;

namespace SqueakSqueak.Saving
{
    /// <summary>실제 IO 경계만 대체해 쓰기/교체 실패를 격리 검사합니다. 비원자 대체 구현은 없습니다.</summary>
    public class SaveFileOperations
    {
        public virtual bool Exists(string path)
        {
            try{var attributes=File.GetAttributes(path);if((attributes&FileAttributes.Directory)!=0)throw new IOException("Save file path is a directory.");return true;}
            catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}
        }
        public virtual byte[] Read(string path)
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
            if(stream.Length>CoinSaveLimits.MaximumFileBytes)throw new SaveDataException(SaveIssue.Unsupported,"Save file exceeds supported limit; do not overwrite");
            var bytes=new byte[(int)stream.Length];var position=0;
            while(position<bytes.Length){var n=stream.Read(bytes,position,bytes.Length-position);if(n==0)throw new EndOfStreamException();position+=n;}return bytes;
        }
        public virtual void WriteNew(string path,byte[] bytes)
        {using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        public virtual void CommitNew(string temporary,string primary)=>File.Move(temporary,primary);
        public virtual void Replace(string temporary,string primary,string previous)=>File.Replace(temporary,primary,previous);
        public virtual void Preserve(string source,string destination)=>File.Move(source,destination);
        public virtual IDisposable Lock(string path)=>new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    internal sealed class UpdateDecision<T>
    {
        internal T Data;internal SaveWriteStatus? Early;internal string Message;
        internal static UpdateDecision<T> Write(T data)=>new(){Data=data};
        internal static UpdateDecision<T> Return(SaveWriteStatus status,string message)=>new(){Early=status,Message=message};
    }
    internal sealed class SafeSaveFileStore<T>
    {
        private readonly string directory,primary,backup;
        private readonly SaveCodec<T> codec;
        private readonly SaveFileOperations files;
        internal SafeSaveFileStore(string directory,string file,SaveCodec<T> codec,SaveFileOperations files)
        {
            if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathFullyQualified(directory))throw new ArgumentException("Explicit absolute save directory required.");
            this.directory=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(this.directory.Length<=Path.GetPathRoot(Path.GetFullPath(directory)).TrimEnd(Path.DirectorySeparatorChar).Length)throw new ArgumentException("Filesystem root is not a save directory.");
            this.codec=codec;this.files=files??new SaveFileOperations();primary=Path.Combine(this.directory,file);backup=primary+".bak";
        }
        private SaveReadResult<T> ReadOne(string path,SaveSource source)
        {
            try
            {
                if(!files.Exists(path))return new SaveReadResult<T>(SaveIssue.Missing,SaveSource.None,"Save missing");
                var document=codec.Decode(files.Read(path));return new SaveReadResult<T>(SaveIssue.None,source,null,document.Data,document.Revision);
            }
            catch(SaveDataException e){return new SaveReadResult<T>(e.Issue,source,e.Message);}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is System.Security.SecurityException||e is NotSupportedException)
            {return new SaveReadResult<T>(SaveIssue.IoError,source,e.Message);}
        }
        private static bool BlocksRecovery(SaveIssue issue)=>issue!=SaveIssue.None&&issue!=SaveIssue.Missing&&issue!=SaveIssue.Corrupt;
        private SaveReadResult<T> Select(out SaveReadResult<T> main,out SaveReadResult<T> previous)
        {
            main=ReadOne(primary,SaveSource.Primary);previous=ReadOne(backup,SaveSource.Backup);
            // 미래/정의 불일치/접근 실패는 손상으로 취급해 옛 백업으로 다운그레이드하지 않습니다.
            if(BlocksRecovery(main.Issue))return main;if(BlocksRecovery(previous.Issue))return previous;
            if(main.HasData)return previous.Issue==SaveIssue.Corrupt?new SaveReadResult<T>(SaveIssue.None,SaveSource.Primary,"Backup corrupt; primary valid, original backup preserved",main.Data,main.Revision):main;
            if(previous.HasData)return new SaveReadResult<T>(SaveIssue.None,SaveSource.Backup,"Recovered from valid backup; primary preserved",previous.Data,previous.Revision);
            if(main.Issue==SaveIssue.Missing&&previous.Issue==SaveIssue.Missing)return main;
            return new SaveReadResult<T>(SaveIssue.Corrupt,SaveSource.None,"No valid committed save. Files preserved; automatic reset forbidden.");
        }
        internal SaveReadResult<T> Load()=>Select(out _,out _);
        internal SaveWriteResult Update(Func<T> empty,Func<T,long,UpdateDecision<T>> decide)
        {
            var attemptedCommit=false;
            var encoded=false;
            try
            {
                Directory.CreateDirectory(directory);
                using var lease=files.Lock(primary+".lock");
                var current=Select(out var main,out var previous);
                if(!current.HasData&&current.Issue!=SaveIssue.Missing)return new SaveWriteResult(SaveWriteStatus.Rejected,current.Issue+": "+current.Message);
                var decision=decide(current.HasData?current.Data:empty(),current.Revision);
                if(decision.Early.HasValue)return new SaveWriteResult(decision.Early.Value,decision.Message,current.Revision);
                if(current.Revision==long.MaxValue)return new SaveWriteResult(SaveWriteStatus.Rejected,"Revision exhausted");
                var revision=current.Revision+1;var bytes=codec.Encode(decision.Data,revision);
                encoded=true;
                var temporary=primary+".tmp-"+Guid.NewGuid().ToString("N");files.WriteNew(temporary,bytes);
                // 닫힌 임시 파일도 다시 읽어 검증합니다. 잘못된 쓰기는 정상본 교체 전에 멈춥니다.
                var verified=files.Read(temporary);codec.Decode(verified);
                if(!System.Linq.Enumerable.SequenceEqual(bytes,verified))throw new IOException("Temporary write differs from candidate.");
                if(previous.Issue==SaveIssue.Corrupt)files.Preserve(backup,backup+".corrupt-"+Guid.NewGuid().ToString("N"));
                attemptedCommit=true;
                if(main.Issue==SaveIssue.Missing)files.CommitNew(temporary,primary);
                else files.Replace(temporary,primary,main.Issue==SaveIssue.None?backup:primary+".corrupt-"+Guid.NewGuid().ToString("N"));
                return new SaveWriteResult(SaveWriteStatus.Saved,null,revision);
            }
            catch(SaveDataException e){return new SaveWriteResult(encoded?SaveWriteStatus.Failed:SaveWriteStatus.Rejected,e.Issue+": "+e.Message);}
            catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is System.Security.SecurityException||e is NotSupportedException)
            {return new SaveWriteResult(attemptedCommit?SaveWriteStatus.Indeterminate:SaveWriteStatus.Failed,e.Message);}
        }
    }
}
