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
        private readonly GameObject entry,root,dialog;
        private readonly Text progress,info,dialogText;
        private readonly Image fill;
        private readonly Button store,scanner;
        private readonly Button[] toolButtons=new Button[3];
        private bool noticeOnly;
        private static readonly Color Panel=new(.06f,.08f,.11f,.94f),Normal=new(.12f,.2f,.25f,1),Selected=new(.04f,.5f,.6f,1);

        public CleaningExperimentView(Canvas canvas,Font font,CleaningExperimentController owner)
        {
            this.owner=owner;this.font=font;
            entry=Rect("RB.Entry",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            Button("실험 M1",entry.transform,new Vector2(-85,-152),new Vector2(150,40),()=>owner.RequestCoin(0),new Vector2(.5f,1));
            Button("실험 M2",entry.transform,new Vector2(85,-152),new Vector2(150,40),()=>owner.RequestCoin(1),new Vector2(.5f,1));
            root=Rect("RB.UI",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            var top=Box("RB.Header",root.transform,new Vector2(0,-62),new Vector2(780,110),new Vector2(.5f,1));
            info=Label("RB.Info",top.transform,new Vector2(0,34),new Vector2(760,30),17);
            Label("RB.Controls",top.transform,new Vector2(0,6),new Vector2(760,28),16).text="칫솔·천: 누른 채 문지르기 / 스포이드: 클릭 / MMB 회전 / Wheel 확대 / F 복원";
            progress=Label("RB.ProgressText",top.transform,new Vector2(0,-23),new Vector2(760,28),20);
            var track=Box("RB.Gauge",root.transform,new Vector2(0,-130),new Vector2(600,14),new Vector2(.5f,1));
            fill=Rect("RB.GaugeFill",track.transform,Vector2.zero,Vector2.zero,true).gameObject.AddComponent<Image>();
            fill.color=new Color(.1f,.72f,.8f);fill.raycastTarget=false;fill.rectTransform.anchorMax=new Vector2(0,1);
            scanner=Button("스캐너 꺼짐",root.transform,new Vector2(-106,-46),new Vector2(180,42),()=>owner.ToggleScanner(),new Vector2(1,1));
            Button("실험 종료",root.transform,new Vector2(-106,-98),new Vector2(180,42),()=>owner.RequestLeave(),new Vector2(1,1));
            Button("앱 종료",root.transform,new Vector2(-106,-150),new Vector2(180,42),()=>owner.RequestQuit(),new Vector2(1,1));
            for(var i=0;i<3;i++){var index=i;toolButtons[i]=Button("도구",root.transform,new Vector2((i-1)*176,55),new Vector2(164,62),()=>owner.SelectTool(index),new Vector2(.5f,0));}
            store=Button("보관 확인",root.transform,new Vector2(-112,55),new Vector2(190,62),()=>owner.RequestStore(owner.RoundId),new Vector2(1,0));
            Button("새 M1",root.transform,new Vector2(89,81),new Vector2(140,40),()=>owner.RequestCoin(0),new Vector2(0,0));
            Button("새 M2",root.transform,new Vector2(89,31),new Vector2(140,40),()=>owner.RequestCoin(1),new Vector2(0,0));
            dialog=Rect("RB.Dialog",canvas.transform,Vector2.zero,Vector2.zero,true).gameObject;
            var shade=dialog.AddComponent<Image>();shade.color=new Color(0,0,0,.8f);shade.raycastTarget=true;
            var card=Box("RB.DialogCard",dialog.transform,Vector2.zero,new Vector2(640,280),new Vector2(.5f,.5f));
            dialogText=Label("RB.DialogText",card.transform,new Vector2(0,45),new Vector2(600,120),21);
            Button("확인",card.transform,new Vector2(-110,-82),new Vector2(180,48),()=>{if(noticeOnly)HideDialog();else owner.ConfirmDialog(owner.DialogRoundId);},new Vector2(.5f,.5f));
            Button("취소",card.transform,new Vector2(110,-82),new Vector2(180,48),()=>{if(noticeOnly)HideDialog();else owner.CancelDialog(owner.DialogRoundId);},new Vector2(.5f,.5f));
            dialog.SetActive(false);root.SetActive(false);
        }

        public void Refresh(bool active,ExperimentState state,int percent,float elapsed,int stored,int limit,string coin,
            CleaningToolDefinition selected,bool scan,IReadOnlyList<CleaningToolDefinition> tools,float gaugeValue)
        {
            entry.SetActive(!active);root.SetActive(active);if(!active)return;
            var seconds=Mathf.FloorToInt(elapsed);
            info.text=$"{coin} · 시험용 보관 · 종료 시 사라짐 ({stored}/{limit})";
            progress.text=$"진행 {percent}% · 작업 {seconds/60:00}:{seconds%60:00}";
            // sprite 없는 Image의 Filled fallback을 쓰지 않고 실제 사각형 폭을 변경합니다.
            fill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(gaugeValue),1);
            store.interactable=state==ExperimentState.Ready;
            store.GetComponentInChildren<Text>().text=state==ExperimentState.Stored?"시험용 보관됨":"보관 확인";
            scanner.interactable=state==ExperimentState.Cleaning||state==ExperimentState.Ready||state==ExperimentState.Stored;
            scanner.GetComponentInChildren<Text>().text=scan?"스캐너 켜짐":"스캐너 꺼짐";
            for(var i=0;i<toolButtons.Length;i++)
            {
                toolButtons[i].GetComponentInChildren<Text>().text=tools[i].DisplayName;
                toolButtons[i].interactable=state==ExperimentState.Cleaning||state==ExperimentState.Ready;
                toolButtons[i].GetComponent<Image>().color=selected==tools[i]?Selected:Normal;
            }
        }
        public void ShowDialog(string text){noticeOnly=false;dialogText.text=text;dialog.SetActive(true);dialog.transform.SetAsLastSibling();}
        public void ShowNotice(string text){ShowDialog(text);noticeOnly=true;}
        public void HideDialog()=>dialog.SetActive(false);
        public float GaugeAnchor => fill.rectTransform.anchorMax.x;
        public void SetSuspended(bool suspended)
        {
            foreach(var go in new[]{entry,root,dialog})
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
        public void Dispose(){if(entry!=null)UnityEngine.Object.Destroy(entry);if(root!=null)UnityEngine.Object.Destroy(root);if(dialog!=null)UnityEngine.Object.Destroy(dialog);}
    }
}
