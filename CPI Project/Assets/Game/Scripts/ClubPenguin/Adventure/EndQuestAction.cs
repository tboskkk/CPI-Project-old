using ClubPenguin.Analytics;
using Disney.MobileNetwork;
using HutongGames.PlayMaker;

namespace ClubPenguin.Adventure
{
	[ActionCategory("Quest")]
	public class EndQuestAction : FsmStateAction
	{
		public override void OnEnter()
		{
			UnityEngine.Debug.Log("[QUESTDIAG] END QUEST fsm=" + base.Fsm.Name + " go=" + base.Fsm.GameObjectName + " state=" + base.State.Name + " prev=" + (base.Fsm.PreviousActiveState != null ? base.Fsm.PreviousActiveState.Name : "none") + " event=" + (base.Fsm.LastTransition != null ? base.Fsm.LastTransition.EventName : "none") + " autoSync=" + UnityEngine.Physics.autoSyncTransforms + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " t=" + UnityEngine.Time.realtimeSinceStartup.ToString("F2"));
			Service.Get<ICPSwrveService>().Action("game.quest", "complete", base.Fsm.Name);
			Service.Get<QuestService>().EndQuest(base.Owner, base.Fsm.Name);
			Finish();
		}
	}
}
