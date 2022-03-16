using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.Math;


//I hate C#

namespace FastPursuitV {
    
    //Main script, core function of this plugin, enables cop to engage in high speed pursuit
    public class Main : Script {
        public Main() {
           Tick += OnTick;
           Interval = 1;
        }

        private int tickcount = 0;
        //private int PersistentCopCount = 0;

        void OnTick(object sender, System.EventArgs e) {

            tickcount++;

            Player PlayerInstance = Game.Player;
            Ped PlayerPed = Game.Player.Character;

            //Check if player is avaliable
            if (PlayerInstance == null || PlayerPed == null) { return; }
            if (!PlayerPed.Exists() || PlayerPed.IsDead || !PlayerInstance.CanControlCharacter) { return; }

            if (tickcount % 10000 == 0) {
                Ped[] PedPool = World.GetAllPeds();

                foreach (Ped CurrentPed in PedPool) {
                    if (CurrentPed.RelationshipGroup.GetHashCode() == Game.GenerateHash("COP")) {
                        if (CurrentPed.IsPersistent && !CurrentPed.IsInVehicle()) CurrentPed.IsPersistent = false;
                    }
                }
            }

            //Vehicle pool is limited to only 4 small cop vehicle models
            //For the sake of performance since C# and SHV.net sucks performance wise
            //And could be pretty disturbing seeing huge police van chasing after your ass 200KMPH+, so those vehicles models are not included
            Vehicle[] VehiclePool = World.GetAllVehicles(new List<Model> { new Model("police"), new Model("police2"), new Model("police3"), new Model("sheriff") }.ToArray());

            if (VehiclePool == null) return;

            foreach (Vehicle CurrentVehicle in VehiclePool) {
                
                if (CurrentVehicle == null) continue;
                if (!CurrentVehicle.Exists()) continue;

                Ped Driver = CurrentVehicle.Driver;
                if (Driver == null) continue;

                if (Driver.Exists()) {

                    
                    //Check if driver is a cop
                    if (Driver.RelationshipGroup.GetHashCode() == Game.GenerateHash("COP")) {

                        //Set driver behaviors
                        if (tickcount % 3000 == 0) {
                            Driver.DrivingSpeed = 280;
                            Driver.MaxDrivingSpeed = 360;
                            Function.Call(Hash.SET_PED_SEEING_RANGE, Driver, 100.0f);
                            Function.Call(Hash.SET_TASK_VEHICLE_CHASE_IDEAL_PURSUIT_DISTANCE, Driver.Handle, 1.0);
                            Function.Call(Hash.SET_DRIVER_ABILITY, Driver.Handle, 1.0);
                            Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, Driver.Handle, 1.0);

                            //Pimp the cop ride
                            CurrentVehicle.EnginePowerMultiplier = 100;
                            CurrentVehicle.ThrottlePower = 1;
                            CurrentVehicle.Turbo = 3;
                            CurrentVehicle.IsAxlesStrong = true;
                            if (Function.Call<int>(Hash.GET_VEHICLE_MOD_KIT, CurrentVehicle) != 0) {
                                CurrentVehicle.CanTiresBurst = false;
                                Function.Call(Hash.SET_VEHICLE_MOD_KIT, CurrentVehicle.Handle, 0);
                                for (int mod = 0; mod < 17; mod++) {
                                    uint modIndex = Function.Call<uint>(Hash.GET_NUM_VEHICLE_MODS, CurrentVehicle.Handle, mod) - 1;
                                    Function.Call(Hash.SET_VEHICLE_MOD, CurrentVehicle.Handle, mod, modIndex, 0);
                                }
                            }


                        }


                        //if player isnt wanted we can skip the rest of the script
                        if (PlayerInstance.WantedLevel < 1) continue;
                        else if (PlayerInstance.WantedLevel >= 3) Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.2f);

                        Vector3 VehiclePosition = CurrentVehicle.Position;
                        float VehicleMovementSpeed = CurrentVehicle.Speed * 3.6f;
                        float VehicleForwardVelocity = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, CurrentVehicle.Handle, true).Y * 3.6f;
                        float VehicleSteeringAngle = CurrentVehicle.SteeringAngle;
                        float Distance = Vector3.Distance(VehiclePosition, PlayerPed.Position);

                        if (CurrentVehicle.IsOnAllWheels) {
                            CurrentVehicle.ApplyForceRelative(new Vector3(VehicleSteeringAngle * VehicleMovementSpeed * -0.00005f, Math.Abs(VehicleSteeringAngle) * VehicleForwardVelocity * -0.00003f, -0.1f + Math.Abs(VehicleSteeringAngle) * VehicleMovementSpeed * -0.00005f), new Vector3(0, 0, 0), ForceType.MaxForceRot);
                        }


                        //Prevent despawn of cops
                        if (Driver.IsDead || CurrentVehicle.IsDead || Distance > 400) {
                            if (CurrentVehicle.IsPersistent || Driver.IsPersistent) {

                                CurrentVehicle.IsPersistent = false;
                                Driver.IsPersistent = false;
                            }
                        }
                        else if (!CurrentVehicle.IsPersistent || !Driver.IsPersistent) {
                            CurrentVehicle.IsPersistent = true;
                            Driver.IsPersistent = true;
                        }


                        if (VehicleForwardVelocity > 180)
                            CurrentVehicle.BrakePower = 6;
                        else if (VehicleForwardVelocity > 120)
                            CurrentVehicle.BrakePower = 4;
                        else
                            CurrentVehicle.BrakePower = 1;


                        //Apply external force to help cop around corners
                       

                        /*
                        Speed boost
                        Attempt to apply boost if:

                        All the wheels is on ground
                        Forward velocity is faster than 120 KMPH
                        Not turning sharp
                        15m away from the Target

                        Following conditions is checked every 500 ticks (500ms interval)
                         */
                        if (CurrentVehicle.IsOnAllWheels && VehicleForwardVelocity > 120 && Math.Abs(VehicleSteeringAngle) < 5 && tickcount % 500 == 0) {
                            //Setup a trace in front of the vehicle

                            VehiclePosition.Z += 1f;
                            Vector3 TraceEndPoint = VehiclePosition + CurrentVehicle.ForwardVector * 30.0f;
                            RaycastResult Trace = World.RaycastCapsule(VehiclePosition, TraceEndPoint, 1f, IntersectFlags.Everything, CurrentVehicle);
                            
                            //Check the ground height at the end point of the line trace (to avoid vehicles flying off the cliff)
                            float GroundHeightAhead = World.GetGroundHeight(Trace.DidHit ? Trace.HitPosition : TraceEndPoint);
                            bool OnRoad = Function.Call<bool>(Hash.IS_POINT_ON_ROAD, Trace.DidHit ? Trace.HitPosition.X : TraceEndPoint.X, Trace.DidHit ? Trace.HitPosition.Y : TraceEndPoint.Y, GroundHeightAhead, CurrentVehicle);
                            
                            if ((Trace.DidHit ? Trace.HitPosition.Z : TraceEndPoint.Z) - GroundHeightAhead < 3 && OnRoad) {
                                //The speed is scaled based on the distance away from the obstacles ahead
                                //If nothing is detected 60m in front of the vehicle, the speed would be set to 300 KMPH (pretty fast)
                                float DistanceScale = Vector3.Distance(VehiclePosition, Trace.HitPosition) / Vector3.Distance(VehiclePosition, TraceEndPoint);
                                if(Distance < 30) DistanceScale = Distance / Vector3.Distance(VehiclePosition, TraceEndPoint);
                                
                                if (Trace.DidHit) {

                                    if (DistanceScale < 0.3) DistanceScale = 0.5f;
                                    else if (DistanceScale > 1) DistanceScale = 1;
                                    if (VehicleForwardVelocity < 85 * DistanceScale * 3.6)
                                        CurrentVehicle.ForwardSpeed = 85 * DistanceScale;
                                  

                                }
                                else {
                                    if (VehicleForwardVelocity < 85 * 3.6)
                                        CurrentVehicle.ForwardSpeed = 85;
                                }

                            }
                        }

                       





                    }

                    //end of loop
                }
                else if (CurrentVehicle.IsPersistent) CurrentVehicle.IsPersistent = false;
            }

        }
        void ent() {
            
        }
    }
}
