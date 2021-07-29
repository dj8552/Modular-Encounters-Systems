using ModularEncountersSystems.Entities;
using ModularEncountersSystems.Helpers;
using ModularEncountersSystems.Logging;
using ModularEncountersSystems.Watchers;
using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;
using VRageMath;

namespace ModularEncountersSystems.Behavior.Subsystems {

	public class DespawnSystem{

		private IBehavior _behavior;

		public bool UsePlayerDistanceTimer;
		public int PlayerDistanceTimerTrigger;
		public double PlayerDistanceTrigger;

		public bool UseNoTargetTimer;
		public int NoTargetTimerTrigger;

		public bool UseRetreatTimer;
		public int RetreatTimerTrigger;
		public double RetreatDespawnDistance;

		public IMyRemoteControl RemoteControl;

		public int PlayerDistanceTimer; //Storage
		public double PlayerDistance;
		public PlayerEntity NearestPlayer;

		public int RetreatTimer; //Storage

		public bool SuspendNoTargetTimer = false;
		public int NoTargetTimer;

		public bool NoTargetExpire;
		
		public event Action RetreatTriggered;

		private bool _mesDespawnTriggerCheck;

		public DespawnSystem(IBehavior behavior, IMyRemoteControl remoteControl = null) {

			UsePlayerDistanceTimer = true;
			PlayerDistanceTimerTrigger = 150;
			PlayerDistanceTrigger = 25000;

			UseNoTargetTimer = false;
			NoTargetTimerTrigger = 60;

			UseRetreatTimer = false;
			RetreatTimerTrigger = 600;
			RetreatDespawnDistance = 3000;

			RemoteControl = null;

			PlayerDistanceTimer = 0;
			PlayerDistance = 0;

			RetreatTimer = 0;

			NoTargetTimer = 0;

			NoTargetExpire = false;

			Setup(remoteControl);
			_behavior = behavior;


		}
		
		private void Setup(IMyRemoteControl remoteControl){

			if(remoteControl == null || MyAPIGateway.Entities.Exist(remoteControl?.SlimBlock?.CubeGrid) == false) {

				return;

			}

			this.RemoteControl = remoteControl;

		}

		public void InitTags() {

			if (string.IsNullOrWhiteSpace(RemoteControl.CustomData) == false) {

				var descSplit = RemoteControl.CustomData.Split('\n');

				foreach (var tag in descSplit) {

					//UsePlayerDistanceTimer
					if (tag.Contains("[UsePlayerDistanceTimer:") == true) {

						TagParse.TagBoolCheck(tag, ref UsePlayerDistanceTimer);

					}

					//PlayerDistanceTimerTrigger
					if (tag.Contains("[PlayerDistanceTimerTrigger:") == true) {

						TagParse.TagIntCheck(tag, ref this.PlayerDistanceTimerTrigger);

					}

					//PlayerDistanceTrigger
					if (tag.Contains("[PlayerDistanceTrigger:") == true) {

						TagParse.TagDoubleCheck(tag, ref this.PlayerDistanceTrigger);

					}

					//UseNoTargetTimer
					if (tag.Contains("[UseNoTargetTimer:") == true) {

						TagParse.TagBoolCheck(tag, ref UseNoTargetTimer);

					}

					//NoTargetTimerTrigger
					if (tag.Contains("[NoTargetTimerTrigger:") == true) {

						TagParse.TagIntCheck(tag, ref this.NoTargetTimerTrigger);

					}

					//UseRetreatTimer
					if (tag.Contains("[UseRetreatTimer:") == true) {

						TagParse.TagBoolCheck(tag, ref UseRetreatTimer);

					}

					//RetreatTimerTrigger
					if (tag.Contains("[RetreatTimerTrigger:") == true) {

						TagParse.TagIntCheck(tag, ref this.RetreatTimerTrigger);

					}

					//RetreatDespawnDistance
					if (tag.Contains("[RetreatDespawnDistance:") == true) {

						TagParse.TagDoubleCheck(tag, ref this.RetreatDespawnDistance);

					}

				}

			}

		}

		public void ProcessTimers(BehaviorMode mode, bool invalidTarget = false){
			
			if(this.RemoteControl == null){
				
				return;
				
			}

			this.NearestPlayer = PlayerManager.GetNearestPlayer(this.RemoteControl.GetPosition());

			if(mode == BehaviorMode.Retreat) {

				if(this.NearestPlayer.ActiveEntity()) {

					if(Vector3D.Distance(this.RemoteControl.GetPosition(), this.NearestPlayer.GetPosition()) > this.RetreatDespawnDistance){

						BehaviorLogger.Write("Retreat Despawn: Player Far Enough", BehaviorDebugEnum.Despawn);
						_behavior.Settings.DoDespawn = true;
						
					}

				} else {

					BehaviorLogger.Write("Retreat Despawn: No Player", BehaviorDebugEnum.Despawn);
					_behavior.Settings.DoDespawn = true;

				}

			}
			
			if(this.UsePlayerDistanceTimer == true){
				
				if(this.NearestPlayer == null){
					
					PlayerDistanceTimer++;
					
				}else if(this.NearestPlayer.ActiveEntity()) {

					if(Vector3D.Distance(this.NearestPlayer.GetPosition(), this.RemoteControl.GetPosition()) > this.PlayerDistanceTrigger) {
						
						PlayerDistanceTimer++;

						if(PlayerDistanceTimer >= PlayerDistanceTimerTrigger) {

							BehaviorLogger.Write("No Player Within Distance", BehaviorDebugEnum.Despawn);
							_behavior.Settings.DoDespawn = true;

						}
						
					}else{
						
						PlayerDistanceTimer = 0;
						
					}
					
				}
				
			}

			if(this.UseNoTargetTimer == true && this.SuspendNoTargetTimer == false) {

				if(invalidTarget == true) {

					this.NoTargetTimer++;

					if(this.NoTargetTimer >= this.NoTargetTimerTrigger) {

						this.NoTargetExpire = true;

					}

				} else {

					this.NoTargetTimer = 0;

				}

			}
			
			if(this.UseRetreatTimer == true && _behavior.Settings.DoRetreat == false){
				
				RetreatTimer++;

				if(RetreatTimer >= RetreatTimerTrigger) {

					_behavior.Settings.DoRetreat = true;

				}
				
			}

			if (_behavior.Settings.DoDespawn) {
				_behavior.Trigger.ProcessDespawnTriggers();
				DespawnGrid();
			
			}
		
		}
		
		public void Retreat(){

			_behavior.Trigger.ProcessRetreatTriggers();
			BehaviorLogger.Write("Retreat Signal Received For Grid: " + this.RemoteControl.SlimBlock.CubeGrid.CustomName, BehaviorDebugEnum.Despawn);
			_behavior.Settings.DoRetreat = true;
			
		}

		public void DespawnGrid() {

			if (_behavior.CurrentGrid != null) {

				_behavior.CurrentGrid.RefreshSubGrids();

				foreach (var grid in _behavior.CurrentGrid.LinkedGrids) {

					Cleaning.RemoveGrid(grid);

					if (grid.Npc != null)
						grid.Npc.DespawnSource = "Despawn-Behavior";

				}
			
			}

			BehaviorLogger.Write("Despawning Grid: " + this.RemoteControl.SlimBlock.CubeGrid.CustomName, BehaviorDebugEnum.Despawn);

			MyAPIGateway.Utilities.InvokeOnGameThread(() => {

				var gridGroup = MyAPIGateway.GridGroups.GetGroup(this.RemoteControl.SlimBlock.CubeGrid, GridLinkTypeEnum.Logical);

				foreach(var grid in gridGroup) {

					if(grid.MarkedForClose == false) {

						grid.Close();

					}

				}

			});

		}
		
	}
	
}