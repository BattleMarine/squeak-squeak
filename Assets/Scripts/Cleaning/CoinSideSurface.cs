using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>원통 chart: U 둘레 반복, V 높이. 월드 원형 footprint는 펼친 호 길이/높이로 계산합니다.</summary>
    public static class CoinSideSurface
    {
        public static float DeltaU(float from,float to)=>Mathf.Repeat(to-from+.5f,1)-.5f;
        public static CoinSurfaceContact Contact(Matrix4x4 matrix,Bounds bounds,float u,float v)
        {
            u=Mathf.Repeat(u,1);v=Mathf.Clamp01(v);var angle=u*2*Mathf.PI;
            var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var local=bounds.center+radial*bounds.extents.x+Vector3.up*((v*2-1)*bounds.extents.y);
            return new CoinSurfaceContact(CoinSurfaceFace.Side,new Vector2(u,v),local,matrix.MultiplyPoint3x4(local),matrix.inverse.transpose.MultiplyVector(radial).normalized,bounds.extents.x);
        }
        public static float Distance(Matrix4x4 matrix,CoinSurfaceContact from,CoinSurfaceContact to)
        {
            if(from.SideRadius<=0||to.SideRadius!=from.SideRadius)return float.PositiveInfinity;
            var angle=from.Uv.x*2*Mathf.PI;var delta=DeltaU(from.Uv.x,to.Uv.x)*2*Mathf.PI;
            // Simpson 표본은 양의 직교 Scale의 타원 원통 호도 지원합니다. 원형에서는 정확한 상수 integrand입니다.
            float Speed(float a)=>matrix.MultiplyVector(new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*from.SideRadius).magnitude;
            var sum=Speed(angle)+Speed(angle+delta);
            for(var i=1;i<8;i++)sum+=(i%2==0?2:4)*Speed(angle+delta*i/8);
            var arc=Mathf.Abs(delta)*sum/24;
            var height=matrix.MultiplyVector(Vector3.up*(to.LocalPosition.y-from.LocalPosition.y)).magnitude;
            return Mathf.Sqrt(arc*arc+height*height);
        }
    }
}
