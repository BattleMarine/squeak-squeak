using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Experiment
{
    /// <summary>실험 상태를 소유하지 않는 임시 uGUI. 런타임 생성물만 파괴합니다.</summary>
    public sealed class CleaningExperimentView : IDisposable
    {
        private readonly CleaningExperimentController owner;
        private readonly Font font;
        private readonly GameObject entry,root,dialog,library;
        private readonly Text progress,info,dialogText;
        private readonly Image fill;
        private readonly Button store,scanner,confirm,cancel;
        private readonly RectTransform dialogCard,dialogViewport;
        private readonly ScrollRect dialogScroll;
        private readonly Button[] toolButtons=new Button[3];
        private bool noticeOnly;
        private Action confirmAction,cancelAction;
        private readonly Button[] savedButtons=new Button[16];
        private static readonly Color Panel=new(.06f,.08f,.11f,.94f),Normal=new(.12f,.2f,.25f,1),Selected=new(.04f,.5f,.6f,1);

        public CleaningExperimentView(Canvas canvas,Font font,CleaningExperimentController owner)
        {
            this.owner=owner;this.font=font;
            entry=Rect("RB.Entry",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            Button("실험 M1",entry.transform,new Vector2(-85,-152),new Vector2(150,40),()=>owner.RequestCoin(0),new Vector2(.5f,1));
            Button("실험 M2",entry.transform,new Vector2(85,-152),new Vector2(150,40),()=>owner.RequestCoin(1),new Vector2(.5f,1));
            Button("보관함",entry.transform,new Vector2(250,-152),new Vector2(150,40),()=>owner.RequestLibrary(),new Vector2(.5f,1));
            root=Rect("RB.UI",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            var top=Box("RB.Header",root.transform,new Vector2(0,-62),new Vector2(780,110),new Vector2(.5f,1));
            info=Label("RB.Info",top.transform,new Vector2(0,34),new Vector2(760,30),17);
            Label("RB.Controls",top.transform,new Vector2(0,6),new Vector2(760,28),16).text="칫솔·천: 문지르기 / 스포이드: 클릭 / MMB로 앞·뒤·옆면 회전 / Wheel 확대 / F 복원";
            progress=Label("RB.ProgressText",top.transform,new Vector2(0,-23),new Vector2(760,28),20);
            var track=Box("RB.Gauge",root.transform,new Vector2(0,-130),new Vector2(600,14),new Vector2(.5f,1));
            fill=Rect("RB.GaugeFill",track.transform,Vector2.zero,Vector2.zero,true).gameObject.AddComponent<Image>();
            fill.color=new Color(.1f,.72f,.8f);fill.raycastTarget=false;fill.rectTransform.anchorMax=new Vector2(0,1);
            scanner=Button("스캐너 꺼짐",root.transform,new Vector2(-106,-46),new Vector2(180,42),()=>owner.ToggleScanner(),new Vector2(1,1));
            Button("실험 종료",root.transform,new Vector2(-106,-98),new Vector2(180,42),()=>owner.RequestLeave(),new Vector2(1,1));
            Button("앱 종료",root.transform,new Vector2(-106,-150),new Vector2(180,42),()=>owner.RequestQuit(),new Vector2(1,1));
            Button("보관함",root.transform,new Vector2(-106,-202),new Vector2(180,42),()=>owner.RequestLibrary(),new Vector2(1,1));
            for(var i=0;i<3;i++){var index=i;toolButtons[i]=Button("도구",root.transform,new Vector2((i-1)*176,55),new Vector2(164,62),()=>owner.SelectTool(index),new Vector2(.5f,0));}
            store=Button("보관 확인",root.transform,new Vector2(-112,55),new Vector2(190,62),()=>owner.RequestStore(owner.RoundId),new Vector2(1,0));
            Button("새 M1",root.transform,new Vector2(89,81),new Vector2(140,40),()=>owner.RequestCoin(0),new Vector2(0,0));
            Button("새 M2",root.transform,new Vector2(89,31),new Vector2(140,40),()=>owner.RequestCoin(1),new Vector2(0,0));
            dialog=Rect("RB.Dialog",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            var shade=dialog.AddComponent<Image>();shade.color=new Color(0,0,0,.8f);shade.raycastTarget=true;
            var card=Box("RB.DialogCard",dialog.transform,Vector2.zero,new Vector2(640,280),new Vector2(.5f,.5f));
            dialogCard=(RectTransform)card.transform;
            dialogViewport=Rect("RB.DialogViewport",card.transform,new Vector2(0,45),new Vector2(600,120));
            dialogViewport.gameObject.AddComponent<RectMask2D>();
            var scrollTarget=dialogViewport.gameObject.AddComponent<Image>();scrollTarget.color=Color.clear;
            dialogText=Label("RB.DialogText",dialogViewport,Vector2.zero,new Vector2(600,120),21);
            dialogText.rectTransform.anchorMin=new Vector2(0,1);dialogText.rectTransform.anchorMax=Vector2.one;
            dialogText.rectTransform.pivot=new Vector2(.5f,1);
            dialogText.rectTransform.sizeDelta=new Vector2(0,120);
            dialogScroll=dialogViewport.gameObject.AddComponent<ScrollRect>();dialogScroll.viewport=dialogViewport;
            dialogScroll.content=dialogText.rectTransform;dialogScroll.horizontal=false;dialogScroll.movementType=ScrollRect.MovementType.Clamped;dialogScroll.scrollSensitivity=24;
            confirm=Button("확인",card.transform,new Vector2(-130,-82),new Vector2(230,48),()=>{if(confirmAction!=null)confirmAction();else if(noticeOnly)HideDialog();else owner.ConfirmDialog(owner.DialogRoundId);},new Vector2(.5f,.5f));
            cancel=Button("취소",card.transform,new Vector2(130,-82),new Vector2(230,48),()=>{if(cancelAction!=null)cancelAction();else if(noticeOnly)HideDialog();else owner.CancelDialog(owner.DialogRoundId);},new Vector2(.5f,.5f));
            library=Rect("RB.Library",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            library.AddComponent<Image>().color=Panel;
            var libraryTitle=Label("RB.Library.Title",library.transform,new Vector2(0,-45),new Vector2(1050,50),24);
            libraryTitle.rectTransform.anchorMin=libraryTitle.rectTransform.anchorMax=new Vector2(.5f,1);
            libraryTitle.text="보관함 · 파일에 보관된 결과 (최대16개) · 회전/확대/F 관찰 전용";
            for(var i=0;i<savedButtons.Length;i++)savedButtons[i]=Button("",library.transform,new Vector2(i%2==0?-260:260,-105-i/2*57),new Vector2(500,49),()=>{},new Vector2(.5f,1));
            library.SetActive(false);
            dialog.SetActive(false);root.SetActive(false);
            Canvas.willRenderCanvases+=LayoutDialog;
        }

        public void Refresh(bool active,ExperimentState state,int percent,float elapsed,int stored,int limit,string coin,
            CleaningToolDefinition selected,bool scan,IReadOnlyList<CleaningToolDefinition> tools,float gaugeValue,bool legacySide=false)
        {
            entry.SetActive(!active);root.SetActive(active);if(!active)return;
            var seconds=Mathf.FloorToInt(elapsed);
            info.text=$"{coin} · 영구 보관 ({stored}/{limit})"+(state==ExperimentState.Viewing?" · 관찰 전용":"");
            if(legacySide)info.text+=" · 옆면 없음(이전 결과)";
            progress.text=$"진행 {percent}% · 작업 {seconds/60:00}:{seconds%60:00}";
            // sprite 없는 Image의 Filled fallback을 쓰지 않고 실제 사각형 폭을 변경합니다.
            fill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(gaugeValue),1);
            store.interactable=state==ExperimentState.Ready;
            store.GetComponentInChildren<Text>().text=state==ExperimentState.Stored?"파일 보관됨":state==ExperimentState.Viewing?"관찰 전용":"보관 확인";
            scanner.interactable=state==ExperimentState.Cleaning||state==ExperimentState.Ready||state==ExperimentState.Stored||state==ExperimentState.Viewing;
            scanner.GetComponentInChildren<Text>().text=scan?"스캐너 켜짐":"스캐너 꺼짐";
            for(var i=0;i<toolButtons.Length;i++)
            {
                toolButtons[i].GetComponentInChildren<Text>().text=tools[i].DisplayName;
                toolButtons[i].interactable=state==ExperimentState.Cleaning||state==ExperimentState.Ready;
                toolButtons[i].GetComponent<Image>().color=selected==tools[i]?Selected:Normal;
            }
        }
        public void ShowDialog(string text)
        {
            noticeOnly=false;confirmAction=cancelAction=null;confirm.interactable=cancel.interactable=true;
            confirm.GetComponentInChildren<Text>().text="확인";cancel.GetComponentInChildren<Text>().text="취소";
            dialogText.text=text;dialog.SetActive(true);dialog.transform.SetAsLastSibling();
            LayoutDialog();dialogScroll.StopMovement();dialogScroll.verticalNormalizedPosition=1;
        }
        private void LayoutDialog()
        {
            if(dialog==null||dialogText==null||!dialog.activeSelf)return;
            // 긴 OS 오류도 마지막 복귀/재시도 안내를 잃지 않습니다. 최대 높이 이후에는 스크롤합니다.
            // Canvas scale 변경 시 글꼴 픽셀 반올림 결과도 다시 계산합니다.
            var textHeight=Mathf.Ceil(dialogText.preferredHeight)+2;
            var bodyHeight=Mathf.Clamp(textHeight,120,400);
            var cardHeight=bodyHeight+160;
            if(dialogCard.sizeDelta.y==cardHeight&&dialogText.rectTransform.sizeDelta.y==Mathf.Max(textHeight,bodyHeight))return;
            var scrollPosition=Mathf.Clamp01(dialogScroll.verticalNormalizedPosition);
            dialogCard.sizeDelta=new Vector2(640,cardHeight);dialogViewport.sizeDelta=new Vector2(600,bodyHeight);
            dialogText.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(textHeight,bodyHeight));
            dialogText.rectTransform.anchoredPosition=new Vector2(0,Mathf.Max(0,textHeight-bodyHeight)*(1-scrollPosition));
            confirm.GetComponent<RectTransform>().anchoredPosition=new Vector2(-130,-cardHeight/2+58);
            cancel.GetComponent<RectTransform>().anchoredPosition=new Vector2(130,-cardHeight/2+58);
        }
        public void ShowSystemDialog(string text,Action yes,Action no,string yesText="확인",string noText="취소")
        {
            ShowDialog(text);confirmAction=yes;cancelAction=no;confirm.interactable=yes!=null;cancel.interactable=no!=null;
            confirm.GetComponentInChildren<Text>().text=yesText;cancel.GetComponentInChildren<Text>().text=noText;
        }
        public void ShowLibrary(IReadOnlyList<ExperimentSnapshot> results,Action close)
        {
            for(var i=0;i<savedButtons.Length;i++)
            {
                var button=savedButtons[i];button.onClick.RemoveAllListeners();button.gameObject.SetActive(i<results.Count);
                if(i>=results.Count)continue;var value=results[i];
                button.GetComponentInChildren<Text>().text=$"{i+1:00}. {value.CoinId} · {value.ElapsedSeconds:0.0}초 · 열람";
                button.onClick.AddListener(()=>owner.ViewStoredResult(value.ResultId));
            }
            // 반복 열기에서 소유 버튼을 누적 생성하지 않습니다.
            if(closeLibrary==null)closeLibrary=Button("목록 닫기",library.transform,new Vector2(0,55),new Vector2(240,48),()=>closeLibraryAction?.Invoke(),new Vector2(.5f,0));
            closeLibraryAction=close;library.SetActive(true);library.transform.SetAsLastSibling();
        }
        private Button closeLibrary;
        private Action closeLibraryAction;
        public void HideLibrary()=>library.SetActive(false);
        public void ShowNotice(string text,Action closed)
        {
            ShowDialog(text);noticeOnly=true;
            confirmAction=cancelAction=()=>{HideDialog();closed();};
        }
        public bool HasBlockingOverlay=>dialog.activeSelf||library.activeSelf;
        public void HideDialog()=>dialog.SetActive(false);
        public float GaugeAnchor => fill.rectTransform.anchorMax.x;
        public void SetSuspended(bool suspended)
        {
            foreach(var go in new[]{entry,root,dialog,library})
            {
                // 씬 종료 시 Canvas 자식이 소유자보다 먼저 파괴될 수 있습니다.
                if(go==null)continue;
                var group=go.GetComponent<CanvasGroup>();if(group==null)group=go.AddComponent<CanvasGroup>();
                group.interactable=!suspended;group.blocksRaycasts=!suspended;
            }
        }
        private RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size,bool stretch=false,Vector2? anchor=null)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
            r.anchorMin=stretch?Vector2.zero:anchor??new Vector2(.5f,.5f);r.anchorMax=stretch?Vector2.one:r.anchorMin;
            r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        private GameObject Box(string name,Transform parent,Vector2 position,Vector2 size,Vector2 anchor)
        {var r=Rect(name,parent,position,size,false,anchor);var image=r.gameObject.AddComponent<Image>();image.color=Panel;image.raycastTarget=false;return r.gameObject;}
        private Text Label(string name,Transform parent,Vector2 position,Vector2 size,int fontSize)
        {var r=Rect(name,parent,position,size);var text=r.gameObject.AddComponent<Text>();text.font=font;text.fontSize=fontSize;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.supportRichText=false;return text;}
        private Button Button(string text,Transform parent,Vector2 position,Vector2 size,Action action,Vector2 anchor)
        {
            var r=Rect("RB.Button."+text,parent,position,size,false,anchor);var image=r.gameObject.AddComponent<Image>();image.color=Normal;
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
            Label("RB.Label",r,Vector2.zero,size-new Vector2(10,8),20).text=text;return button;
        }
        public void Dispose(){Canvas.willRenderCanvases-=LayoutDialog;if(entry!=null)UnityEngine.Object.Destroy(entry);if(root!=null)UnityEngine.Object.Destroy(root);if(dialog!=null)UnityEngine.Object.Destroy(dialog);if(library!=null)UnityEngine.Object.Destroy(library);}
    }
}
