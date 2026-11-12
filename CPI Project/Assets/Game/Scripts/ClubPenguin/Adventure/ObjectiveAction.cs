using DevonLocalization.Core;
using Disney.MobileNetwork;
using HutongGames.PlayMaker;

namespace ClubPenguin.Adventure
{
	[ActionCategory("Quest")]
	public class ObjectiveAction : FsmStateAction
	{
		public string Description;

		public string i18nDescription;

		public bool NotifyPlayer = true;

		public int DefinitionRewardIndex = -1;

		public override void OnEnter()
		{
			UnityEngine.Debug.Log("[QUESTDIAG] objective start fsm=" + base.Fsm.Name + " go=" + base.Fsm.GameObjectName + " state=" + base.State.Name + " prev=" + (base.Fsm.PreviousActiveState != null ? base.Fsm.PreviousActiveState.Name : "none") + " event=" + (base.Fsm.LastTransition != null ? base.Fsm.LastTransition.EventName : "none") + " autoSync=" + UnityEngine.Physics.autoSyncTransforms + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " t=" + UnityEngine.Time.realtimeSinceStartup.ToString("F2"));
			string tokenTranslation = Service.Get<Localizer>().GetTokenTranslation(i18nDescription);
			Service.Get<QuestService>().ActiveQuest.StartObjective(base.State.Name, tokenTranslation, NotifyPlayer);
			Finish();
		}

		public override void OnExit()
		{
			string tokenTranslation = Service.Get<Localizer>().GetTokenTranslation(i18nDescription);
			if (base.Fsm.IsSwitchingState)
			{
				Service.Get<QuestService>().ActiveQuest.CompleteObjective(base.State.Name, tokenTranslation);
			}
		}
	}
}
