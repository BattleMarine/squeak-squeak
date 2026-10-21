using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>옆면 chart의 주기 U/높이 clamp footprint. 앞/뒤 chart는 변경하지 않습니다.</summary>
    internal static class CoinSideBrush
    {
        internal static int Apply(CoinSurfaceMaskData data,CoinSurfaceContact center,Matrix4x4 matrix,Bounds bounds,float radius,
            double amount,double polishAmount,CleaningReactionProfile reaction,CleaningToolId tool,bool soften)
        {
            if(!data.HasSide)return 0;
            var n=data.Resolution;var h=data.SideHeight;
            var minimumScale=Mathf.Min(matrix.MultiplyVector(Vector3.right).magnitude,matrix.MultiplyVector(Vector3.forward).magnitude);
            var halfU=radius/(2*Mathf.PI*bounds.extents.x*minimumScale);
            var halfV=radius/(2*bounds.extents.y*matrix.MultiplyVector(Vector3.up).magnitude);
            var left=Mathf.FloorToInt((center.Uv.x-halfU)*n-.5f);var right=Mathf.CeilToInt((center.Uv.x+halfU)*n-.5f);
            if(right-left+1>=n){left=0;right=n-1;}
            var bottom=Mathf.Clamp(Mathf.FloorToInt((center.Uv.y-halfV)*h-.5f),0,h-1);
            var top=Mathf.Clamp(Mathf.CeilToInt((center.Uv.y+halfV)*h-.5f),0,h-1);var changed=0;
            for(var y=bottom;y<=top;y++)for(var rawX=left;rawX<=right;rawX++)
            {
                var x=(rawX%n+n)%n;var u=(x+.5f)/n;var v=(y+.5f)/h;var angle=u*2*Mathf.PI;
                var local=bounds.center+new Vector3(Mathf.Cos(angle)*bounds.extents.x,(2*v-1)*bounds.extents.y,Mathf.Sin(angle)*bounds.extents.x);
                var candidate=new CoinSurfaceContact(CoinSurfaceFace.Side,new Vector2(u,v),local,default,default,bounds.extents.x);
                var distance=CoinSideSurface.Distance(matrix,center,candidate);
                if(distance>radius*(1+1e-5f))continue;
                var index=y*n+x;
                if(soften){if(data.TrySoften(CoinSurfaceFace.Side,index))changed++;continue;}
                var weight=Math.Max(0,1-(double)distance*distance/(radius*radius));weight*=weight;
                var before=data.GetDirt(CoinSurfaceFace.Side,index);
                var coefficient=reaction!=null?reaction.SurfaceCoefficient(tool,data.IsSoftened(CoinSurfaceFace.Side,index)):1;
                var decrease=amount*weight*coefficient;var remaining=Math.Max(0,before-decrease);
                if(data.TrySetDirt(CoinSurfaceFace.Side,index,(float)remaining))changed++;
                var mean=decrease>before?before*before/(2*decrease):(before+remaining)*.5;
                if(data.TrySetPolish(CoinSurfaceFace.Side,index,(float)Math.Min(1,data.GetPolish(CoinSurfaceFace.Side,index)+polishAmount*weight*(1-mean))))changed++;
            }
            return changed;
        }
    }
}
