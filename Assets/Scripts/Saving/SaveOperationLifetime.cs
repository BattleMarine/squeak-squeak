using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SqueakSqueak.Saving
{
    /// <summary>씬 소유자가 사라져도 파일 작업을 취소하지 않으며 정상 종료는 닫기/교체 후 다시 요청합니다.</summary>
    internal sealed class SaveOperationLifetime:MonoBehaviour
    {
        private static Task tail=Task.CompletedTask;
        private static SaveOperationLifetime host;
        private bool quitRequested;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){tail=Task.CompletedTask;host=null;}
        // 메인 스레드 진입만 허용. 이전 씬 작업이 끝난 뒤 다음 세션의 조회/쓰기를 순서대로 시작합니다.
        internal static Task<T> Run<T>(Func<T> work)
        {
            if(host==null){var go=new GameObject("Save.OperationLifetime");DontDestroyOnLoad(go);host=go.AddComponent<SaveOperationLifetime>();}
            var previous=tail;
            var task=Task.Run(async()=>{try{await previous.ConfigureAwait(false);}catch{/* 이전 작업 실패는 다음 읽기를 막지 않습니다. */}return work();});
            tail=task;return task;
        }
        private void OnEnable()=>Application.wantsToQuit+=CanQuit;
        private void OnDestroy(){Application.wantsToQuit-=CanQuit;if(host==this)host=null;}
        private bool CanQuit(){if(tail.IsCompleted)return true;quitRequested=true;return false;}
        private void Update(){if(quitRequested&&tail.IsCompleted){quitRequested=false;Application.Quit();}}
    }
}
