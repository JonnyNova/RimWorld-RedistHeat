﻿using System.Collections.Generic;

using RimWorld;
using UnityEngine;
using Verse;

namespace RedistHeat
{
    public class Building_DuctComp : Building_TempControl
    {
        private const float EqualizationRate = 0.85f;

        private bool isLocked;
        private bool isWorking;
        private bool WorkingState
        {
            set
            {
                isWorking = value;

                if ( isWorking )
                {
                    compPowerTrader.PowerOutput = -compPowerTrader.props.basePowerConsumption;
                }
                else
                {
                    compPowerTrader.PowerOutput = -compPowerTrader.props.basePowerConsumption *
                                                  compTempControl.props.lowPowerConsumptionFactor;
                }

                compTempControl.operatingAtHighPower = isWorking;
            }
        }

        protected CompAirTrader compAir;
        private IntVec3 vecNorth;
        protected Room roomNorth;

        public override string LabelBase => base.LabelBase + " (" + compAir.currentLayer.ToString().ToLower() + ")";

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            compAir = GetComp< CompAirTrader >();
            vecNorth = Position + IntVec3.North.RotatedBy( Rotation );

            Common.WipeExistingPipe( Position );
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.LookValue( ref isLocked, "isLocked", false );
        }

        public override void TickRare()
        {
            if ( vecNorth.Impassable() )
            {
                WorkingState = false;
                return;
            }

            if ( roomNorth == null )
            {
                roomNorth = vecNorth.GetRoom();
                if ( roomNorth == null )
                {
                    WorkingState = false;
                    return;
                }
            }

            if ( !Validate() )
            {
                WorkingState = false;
                return;
            }

            WorkingState = true;

            var connectedNet = compAir.connectedNet;
            roomNorth = (Position + IntVec3.North.RotatedBy( Rotation )).GetRoom();
            float targetTemp;
            if ( roomNorth.UsesOutdoorTemperature )
            {
                targetTemp = roomNorth.Temperature;
            }
            else
            {
                targetTemp = (roomNorth.Temperature*roomNorth.CellCount +
                          connectedNet.NetTemperature*connectedNet.nodes.Count)
                         /(roomNorth.CellCount + connectedNet.nodes.Count);
            }

            compAir.EqualizeWithNets( targetTemp, EqualizationRate );
            if ( !roomNorth.UsesOutdoorTemperature )
            {
                Equalize( roomNorth, targetTemp, EqualizationRate );
            }
        }

        private static void Equalize( Room room, float targetTemp, float rate )
        {
            var tempDiff = Mathf.Abs( room.Temperature - targetTemp );
            var tempRated = tempDiff*rate;
            if ( targetTemp < room.Temperature )
            {
                room.Temperature = Mathf.Max( targetTemp, room.Temperature - tempRated );
            }
            else if ( targetTemp > room.Temperature )
            {
                room.Temperature = Mathf.Min( targetTemp, room.Temperature + tempRated );
            }
        }

        protected virtual bool Validate()
        {
            return !isLocked && compPowerTrader.PowerOn;
        }

        public override void Draw()
        {
            base.Draw();
            if ( isLocked )
            {
                OverlayDrawer.DrawOverlay( this, OverlayTypes.ForbiddenBig );
            }
        }

        public override IEnumerable< Gizmo > GetGizmos()
        {
            foreach ( var g in base.GetGizmos() )
            {
                yield return g;
            }

            var l = new Command_Toggle
            {
                defaultLabel = ResourceBank.StringToggleAirflowLabel,
                defaultDesc = ResourceBank.StringToggleAirflowDesc,
                hotKey = KeyBindingDefOf.CommandItemForbid,
                icon = ResourceBank.UILock,
                groupKey = 912515,
                isActive = () => isLocked,
                toggleAction = () => isLocked = !isLocked
            };
            yield return l;
        }
    }
}