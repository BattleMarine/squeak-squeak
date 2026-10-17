using System;
using System.Collections.Generic;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Experiment
{
    /// <summary>보관 확인 당시의 불변 사본. Unity 텍스처/오브젝트나 원본 배열을 공유하지 않습니다.</summary>
    public sealed class ExperimentSnapshot
    {
        public const int FormatVersion = 1;
        public string ResultId { get; }
        public long RoundId { get; }
        public string CoinId { get; }
        public string ReactionId { get; }
        public string LayoutId { get; }
        public int Resolution { get; }
        public float ElapsedSeconds { get; }
        public double InitialDirtSum { get; }
        public IReadOnlyList<float> A { get; }
        public IReadOnlyList<float> FrontDirt { get; }
        public IReadOnlyList<float> BackDirt { get; }
        public IReadOnlyList<float> FrontPolish { get; }
        public IReadOnlyList<float> BackPolish { get; }
        public IReadOnlyList<bool> FrontSoftened { get; }
        public IReadOnlyList<bool> BackSoftened { get; }

        public ExperimentSnapshot(long round, string coin, string reaction, string layout, CoinSurfaceMaskData mask,
            IReadOnlyList<float> a, float elapsed)
        {
            if (round<=0 || string.IsNullOrEmpty(coin) || string.IsNullOrEmpty(reaction) || string.IsNullOrEmpty(layout) ||
                mask==null || a==null || !float.IsFinite(elapsed) || elapsed<0) throw new ArgumentException("Invalid snapshot source.");
            var acopy = new float[a.Count];
            for(var i=0;i<a.Count;i++)
            { if(!float.IsFinite(a[i])||a[i]<0)throw new ArgumentException("Invalid A."); acopy[i]=a[i]; }
            var fd=new float[mask.TexelCount];var bd=new float[mask.TexelCount];
            var fp=new float[mask.TexelCount];var bp=new float[mask.TexelCount];
            var fs=new bool[mask.TexelCount];var bs=new bool[mask.TexelCount];
            for(var i=0;i<mask.TexelCount;i++)
            {
                fd[i]=mask.GetDirt(CoinSurfaceFace.Front,i);bd[i]=mask.GetDirt(CoinSurfaceFace.Back,i);
                fp[i]=mask.GetPolish(CoinSurfaceFace.Front,i);bp[i]=mask.GetPolish(CoinSurfaceFace.Back,i);
                fs[i]=mask.IsSoftened(CoinSurfaceFace.Front,i);bs[i]=mask.IsSoftened(CoinSurfaceFace.Back,i);
            }
            ResultId=Guid.NewGuid().ToString("N");RoundId=round;CoinId=coin;ReactionId=reaction;LayoutId=layout;
            Resolution=mask.Resolution;ElapsedSeconds=elapsed;InitialDirtSum=mask.InitialDirtSum;
            A=Array.AsReadOnly(acopy);FrontDirt=Array.AsReadOnly(fd);BackDirt=Array.AsReadOnly(bd);
            FrontPolish=Array.AsReadOnly(fp);BackPolish=Array.AsReadOnly(bp);
            FrontSoftened=Array.AsReadOnly(fs);BackSoftened=Array.AsReadOnly(bs);
        }

        /// <summary>검증된 저장 경계에서 ID와 원본 수치를 보존합니다. 배열은 공유하지 않습니다.</summary>
        public static ExperimentSnapshot Restore(string id,long round,string coin,string reaction,string layout,int resolution,float elapsed,double initialSum,
            float[] a,float[] fd,float[] bd,float[] fp,float[] bp,bool[] fs,bool[] bs)
            => new(id,round,coin,reaction,layout,resolution,elapsed,initialSum,a,fd,bd,fp,bp,fs,bs);

        private ExperimentSnapshot(string id,long round,string coin,string reaction,string layout,int resolution,float elapsed,double initialSum,
            float[] a,float[] fd,float[] bd,float[] fp,float[] bp,bool[] fs,bool[] bs)
        {
            if(!Guid.TryParseExact(id,"N",out _)||round<=0||resolution<4||resolution>1024||
                a==null||fd?.Length!=resolution*resolution||bd?.Length!=fd.Length||fp?.Length!=fd.Length||bp?.Length!=fd.Length||fs?.Length!=fd.Length||bs?.Length!=fd.Length)
                throw new ArgumentException("Invalid restored snapshot shape/identity.");
            ResultId=id;RoundId=round;CoinId=coin;ReactionId=reaction;LayoutId=layout;Resolution=resolution;ElapsedSeconds=elapsed;InitialDirtSum=initialSum;
            A=Array.AsReadOnly((float[])a.Clone());FrontDirt=Array.AsReadOnly((float[])fd.Clone());BackDirt=Array.AsReadOnly((float[])bd.Clone());
            FrontPolish=Array.AsReadOnly((float[])fp.Clone());BackPolish=Array.AsReadOnly((float[])bp.Clone());
            FrontSoftened=Array.AsReadOnly((bool[])fs.Clone());BackSoftened=Array.AsReadOnly((bool[])bs.Clone());
        }
    }
}
