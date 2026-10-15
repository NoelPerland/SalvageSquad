using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace SalvageSquad.Tests
{
    public class SliceInputTests
    {
        [UnityTest]
        public IEnumerator SameDragDistanceMovesEquallyAboveAndBelowSquad()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var background=InputSystem.settings.backgroundBehavior;
            var inputMode=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                foreach(float height in new[]{.22f,.6f})
                {
                    run.ResetRun();var start=new Vector2(Screen.width*.4f,Screen.height*height);
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                    yield return null;run.squad.Tick(.02f);
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=start+new Vector2(Screen.width*.2f,0)});
                    yield return null;run.squad.Tick(.02f);
                    Assert.That(run.squad.DesiredX,Is.EqualTo(1.8f).Within(.02f));
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=start+new Vector2(Screen.width*.2f,0)});
                    yield return null;run.squad.Tick(.02f);
                    Assert.That(run.squad.HasSteered,Is.True);
                    for(int i=0;i<40;i++)run.Advance(.02f);
                    Assert.That(run.hint.text,Does.Contain("SHOOT"));
                    Assert.That(run.hint.gameObject.activeSelf,Is.True,"Early steering must not hide the shootable-gate instruction.");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=inputMode;
            }
        }
        [UnityTest]
        public IEnumerator ActiveButtonsAndCancelledDragsCannotMoveSquad()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;
            var background=InputSystem.settings.backgroundBehavior;
            var mode=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                foreach(var button in new[]{run.pauseButton,run.soundButton,run.motionButton})
                {
                    run.ResetRun();var rect=button.GetComponent<RectTransform>();
                    var p=RectTransformUtility.WorldToScreenPoint(run.squad.uiCamera,rect.TransformPoint(rect.rect.center));
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect,p,run.squad.uiCamera),Is.True,button.name);
                    var hit=button.transform.Find("Touch target").GetComponent<RectTransform>();
                    Canvas.ForceUpdateCanvases();Assert.That(hit.rect.height,Is.GreaterThanOrEqualTo(66));
                    p=RectTransformUtility.WorldToScreenPoint(run.squad.uiCamera,rect.TransformPoint(rect.rect.center+Vector2.up*(rect.rect.height*.5f+5)));
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect,p,run.squad.uiCamera),Is.False);
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(hit,p,run.squad.uiCamera),Is.True);
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=p});
                    yield return null;run.squad.Tick(.02f);
                    p.x+=Screen.width*.2f;
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=p});
                    yield return null;run.squad.Tick(.02f);Assert.That(run.squad.DesiredX,Is.Zero,button.name);
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=p});
                    yield return null;run.squad.Tick(.02f);
                }
                run.ResetRun();var start=new Vector2(Screen.width*.5f,Screen.height*.2f);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                yield return null;run.squad.Tick(.02f);
                run.squad.CancelDrag();start.x+=Screen.width*.3f;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=start});
                yield return null;run.squad.Tick(.02f);
                Assert.That(run.squad.DesiredX,Is.Zero,"Focus/pause cancellation must discard the held pointer's jump.");
            }
            finally { InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=mode; }
        }
        [UnityTest]
        public IEnumerator ExpandedPauseTargetAndPrimaryResumeReceiveRealTouchClicks()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();run.enabled=false;run.ResetRun();
            var background=InputSystem.settings.backgroundBehavior;
            var mode=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                Canvas.ForceUpdateCanvases();
                var rect=run.pauseButton.GetComponent<RectTransform>();
                var p=RectTransformUtility.WorldToScreenPoint(run.squad.uiCamera,rect.TransformPoint(rect.rect.center+Vector2.up*(rect.rect.height*.5f+5)));
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=p});
                yield return null;yield return null;run.squad.Tick(.02f);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=p});
                yield return null;yield return null;
                Assert.That(run.IsPaused,Is.True,"Expanded target must deliver a real UI click, not only block steering.");
                Assert.That(run.squad.DesiredX,Is.Zero);
                Canvas.ForceUpdateCanvases();rect=run.replayButton.GetComponent<RectTransform>();
                p=RectTransformUtility.WorldToScreenPoint(run.squad.uiCamera,rect.TransformPoint(rect.rect.center));
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=p});
                yield return null;yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=p});
                yield return null;yield return null;
                Assert.That(run.IsPaused,Is.False);Assert.That(run.Elapsed,Is.Zero);Assert.That(run.squad.UnitCount,Is.EqualTo(5));
            }
            finally { InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=mode; }
        }
        [UnityTest]
        public IEnumerator TouchCanStartOverHiddenReplayAndReleaseWithoutDrift()
        {
            yield return SceneManager.LoadSceneAsync("Scrapline");yield return null;
            var run=Object.FindFirstObjectByType<SliceRun>();
            var background=InputSystem.settings.backgroundBehavior;
            var inputMode=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                Vector2 start=RectTransformUtility.WorldToScreenPoint(run.squad.uiCamera,run.replayButton.transform.position);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                yield return null;yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=start+new Vector2(Screen.width*.2f,0)});
                yield return new WaitForSeconds(.2f);
                Assert.That(run.squad.transform.position.x,Is.GreaterThan(1));
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=start+new Vector2(Screen.width*.2f,0)});
                yield return null;yield return null;
                float goal=run.squad.DesiredX;yield return new WaitForSeconds(.2f);
                Assert.That(run.squad.DesiredX,Is.EqualTo(goal));
            }
            finally
            {
                InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=inputMode;
            }
        }
    }
}
