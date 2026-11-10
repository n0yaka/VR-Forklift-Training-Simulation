# VR-ConTrain Development Log

## 2026-11-05 — Forklift ride test (early Sprint 2/3 prototype)

### Built
- Ride test in `Assets/Scenes/SampleScene.unity`, built by the menu item `VRConTrain > Setup Ride Test Scene` (`RideTestSceneBuilder`): 200 m ground plane with a 2 m checker, one directional light, and the XR rig seated in the cab.
- `ForkliftController`: WheelCollider physics with front-wheel drive and rear-wheel steering. Top speed is 8 km/h forward and 5 km/h in reverse. The front wheels never steer.
- `SteeringWheelInteractable`: an XRI interactable that one or two hands can grab and turn, with a lock of ±360° (one full turn each way). Full lock gives 60° at the rear wheels.
- `ForkliftControls.inputactions` and `ControllerButtonInput`: right trigger accelerates, left trigger brakes, A shifts towards Forward, B shifts towards Reverse, right stick click recenters. Keyboard bindings (W/S, A/D, E/Q, R) are for Editor testing.
- `SeatedRigRecenter`: places the camera at the cab's eye point (0, 1.93, -0.30), measured from side and rear renders of the model.
- `ForkliftGearReadout`: a dashboard letter showing F, N or R, added so the driver can see the gear.
- `ForkliftDriveMath`: pure drive and steering calculations, with EditMode tests.

### Problems and fixes
| Problem | Cause | Fix |
|---|---|---|
| Steering wheel axis unknown | The wheel is tilted but its local axes follow the body | Fitted the axis from the mesh vertices with PCA: tilted exactly 22° from horizontal. Stored as a serialized axis |
| Rear wheels need to both steer and spin, but prefab children can't be reparented | The model is a prefab instance, so steering parents can't be inserted without unpacking it | The controller composes steer, rest rotation and spin into one local rotation |
| Scene builder stopped partway with `MissingReferenceException` | Adding `TextMeshPro` replaces the `Transform` with a `RectTransform`, destroying the saved reference | Add the component first and use its `rectTransform` |
| Hands could not hover the steering wheel | The near caster ignores triggers by default, and XRI removes trigger colliders when it collects an interactable's colliders automatically | Set the casters to `Collide` and register the grab collider in the wheel's `Awake` |
| Pushing the stick would re-enable teleport while seated | `ControllerInputActionManager` turns the teleport interactor back on | Disabled that component, `Locomotion`, the teleport interactors, far casting and the rig's `CharacterController` |

### Test results (Editor Play mode, no headset)
- EditMode: 20/20 passed (`ForkliftDriveMathTests`). Scripts compile with 0 errors and 0 warnings.
- Settling: all four wheels grounded, forklift level at y≈0.001, tyre meshes within 1 mm of the wheel colliders.
- Straight: exactly 8.0 km/h forward and 5.0 km/h in reverse.
- Full right lock, forward: rear wheels at -60°, yaw rate about +45°/s, root sliding sideways at about 1 m/s (tail swing), speed about 4.5 km/h, no tipping.
- Reverse with left lock: yaw rate +30°/s, correct for reversing.
- Brake: stopped from 8 km/h within 2 s. Accelerator in Neutral does not move the forklift.
- Wheel grab, simulated through XRI's real hover and select flow: no hover or grab from far away, hover on the rim, no jump on grab, 1:1 tracking past 180°, stops at the lock, turns back correctly, holds steer 0.250 with the hand still while driving, keeps its angle on release, two hands work together.
- Bindings: resolve on simulated Oculus Touch controllers (trigger 0.75 read as accelerator 0.75, A and B shift gears, readout updates).

### Not yet tested
- **Not yet run on a Quest 3 or through Quest Link.** Real controller tracking, seat position, reach, comfort and frame rate are unverified, so this feature does not yet meet the Definition of Done.

## 2026-11-09 — Mast levers (early Sprint 2/3 prototype)

### Built
- `CabLeverInteractable`: an XRI interactable for the three cab levers. Grab the knob (grip), then pull or push; each lever moves ±15° and springs back to center when released. Partial travel moves the mast proportionally, like a hydraulic valve, with a 10% dead zone.
- `MastController`: moves the mast and forks as kinematic transforms relative to their rest pose.
  - Lift: -0.55 m to +0.90 m from the modelled height, at 0.35 m/s (forks bottom out about 2 cm above the ground).
  - Tilt: 6° forward to 10° back, at 4°/s, hinged at front axle height under the mast.
  - Side-shift: ±0.10 m, at 0.10 m/s.
- Lever mapping:
  - Green (`Elevetor Raise`): pull lifts, push lowers.
  - Blue (`Elevator Tilt`): pull tilts back, push tilts forward.
  - Red (`Elevator Left/Right`): pull shifts the forks left, push shifts them right.
- `ForkliftMastMath` (pure: lever dead zone, rate integration with limits) with 15 EditMode test cases, plus `IForkliftLever`. `ForkliftInputRouter` passes the levers to the mast. `Setup Ride Test Scene` builds all of it.

### Problems and fixes
| Problem | Cause | Fix |
|---|---|---|
| Lever and mast pivots are at the model origin, not at the hinges | Pivots not set in Blender | Hinge and knob found from each lever's mesh (bottom and top vertices). The mast hinge is at front axle height under the mast. Rotation is applied around those points in code, so the model needs no change |
| A hand on the green knob grabbed the steering wheel, and a hand on the blue knob grabbed the red lever | XRI's default near sorting compares pivot positions. All three lever pivots are at the origin (a tie), and the wheel's pivot was nearest | The rig's near casters use `InteractableBased` sorting. Each lever reports its distance to its knob, and the wheel its distance to the nearest point on its rim (0.21 m, measured from the mesh) |
| The test runner froze the Editor | Running EditMode tests with a modified scene opens a modal save dialog | Saved the scene first, then ran the test methods directly |

### Test results (Editor Play mode, simulated hand through XRI hover and select, no headset)
- EditMode: 35/35 passed. Scripts compile with 0 errors and 0 warnings.
- Targeting: each knob is hovered by its own lever, and five points around the rim (including the side next to the levers) are all hovered by the wheel.
- Green: pull raised the forks 0.90 m and stopped at the limit; push lowered them to -0.55 m and stopped. Tilt and side-shift were unchanged.
- Blue: pull tilted the mast back to 10° (mast top towards the driver, fork tips up); push tilted it to -6° forward and stopped.
- Red: pull moved the forks 0.10 m to the driver's left; push moved them 0.10 m right.
- All levers: no jump on grab, the knob followed the hand and stopped at ±15°, the lever returned to center within 0.4 s of release, and the mast held still afterwards.
- Steering wheel after the change: a grab next to the levers took the wheel, a 90° turn gave steer 0.250, and the green lever did not move.

### Not yet tested
- **Not yet run on a Quest 3.** Reach to the knobs, grab feel and the lever speeds are unverified.

## 2026-11-09 — Warehouse environment (early Sprint 2/5 prototype)

### Built
- `WarehouseSceneBuilder` (menu `VRConTrain > Setup Warehouse Environment`, safe to rerun) builds the warehouse in `SampleScene` from Noy's `Warehouse Environment.blend` and `Warehouse Stuff.blend`.
- Prefabs in `Assets/_Project/Prefabs/Warehouse`:
  - `Rack`: static, with a mesh collider so boxes sit on the shelves.
  - `Box`: 30 kg rigidbody.
  - `Cone`: 3 kg rigidbody with a convex hull collider, so it tips over when hit.
  - `Floating Marker`: pivots on the arrow tip.
- Layout in the 46 × 46 m hall:
  - Storage half: 5 rows × 5 racks (one wall row and two back-to-back double rows) with 4.5 m aisles. 196 boxes fill about 60% of the shelf slots (fixed seed), with one drop-off slot left empty.
  - Staging area: 6 floor boxes plus one stacked box.
  - Driving half: the forklift spawns in a corner facing a straight cone lane (10 cones, 3.5 m wide), with a 5-cone slalom (5 m spacing) across the hall.
- Floating markers (Noy's choice for the arrows): bob and spin, pointing down at the end of the cone lane, a pick-up box in the staging area, and the empty rack slot (half size, to fit between the shelves). Script: `FloatingMarker` (new `VRConTrain.Training` assembly).
- Forklift: box colliders on the mast and the fork carriage (plate and 4 tines), so the forks no longer pass through walls, racks and loads. The ride builder no longer resets the forklift to the origin, and skips the test ground plane when the warehouse exists.

### Problems and fixes
| Problem | Cause | Fix |
|---|---|---|
| Unmodified copies of both models were already at the scene root, the building 10 cm off the floor and with no collision | Dragged in for reference | The builder removes loose instances of these two models and builds its own copies |
| A cone hit by the forks was launched 54 m, through the floor and out of the building | The model's floor and walls are single surfaces with no thickness. The tines pinned the cone against the floor, and the push-out correction shot it through | Solid 1 m blocks behind the floor and walls. Cones use continuous collision detection and a 2 m/s push-out cap |
| Racks and props have their pivots at the model origin | Laid out side by side in Blender | Each prefab offsets its mesh child so the pivot is at the base center (at the tip for the arrow), measured from the mesh |

### Test results (Editor Play mode, driver input set from script, no headset)
- Compiles with 0 errors and 0 warnings.
- Spawn: all 4 wheels grounded on the warehouse floor. All 196 shelf boxes are asleep after 8 s; the largest movement was under 2 mm and none fell.
- Cone lane: straight through at 8.0 km/h with 0 cones touched.
- Back wall at full throttle: the fork tips stopped at the wall face (z 22.97) and held for 2 s, with the forklift level.
- Cones hit at 8 km/h by the middle of the forks and by an outer tine: both knocked onto their side and pushed along. None left the hall or went below the floor.
- Forks driven into a rack: the tines entered the bottom bay and pushed 5 boxes back, then the forklift stopped with the mast at the rack face. No box fell.
- Scene totals: 282 renderers, about 57k triangles, 219 rigidbodies.

### Not yet tested
- **Not yet run on a Quest 3.** Frame rate with 219 rigidbodies and 282 renderers is unverified.
- The scene has exponential-squared fog (density 0.05) from before this work. It hides most of the hall beyond about 20 m. Left unchanged pending Noy's decision.

## 2026-11-10 — Walking, teleport, getting in and out (early Sprint 3 prototype)

### Built
- The player now starts **on foot** beside the driver's side of the forklift instead of in the seat. `Setup Ride Test Scene` moves the XR rig out of the seat and sets everything below up.
- On-foot controls (Noy's choices):
  - Room-scale walking with the headset, plus the left stick to walk at 2 m/s (head-relative, with strafe), using the starter rig's move provider.
  - Right stick: 45° snap turn; pull back for 180°.
  - Hold the right trigger to aim a teleport and release it to go. A cyan arc runs from the right hand and Noy's Floating Marker (0.6 scale) bobs over the landing spot. The arc turns red, and no marker shows, on slopes over 30°, surfaces more than 0.5 m above or below the feet (shelves, the forklift roof), or anywhere a 0.25 m × 1.8 m body doesn't fit. The player keeps their heading.
  - A gets in when the head is within 2.5 m of the seat. A billboard "Press A to get in" prompt shows over the cab while in range.
  - Left stick click gets out, only below 0.3 m/s. The driver is placed on the left side of the cab, or on the right or behind it if that side is blocked. "Stop the forklift before getting out" and "No room to get out here" show on a cab hint, as does "Click the left stick to get out" for 4 s after getting in.
- `ForkliftBoarding` switches modes. On foot, the body capsule, locomotion and teleporter are on, and the driver input and seat recenter are off. Seated, it's the other way round, and the rig rides on the seat anchor.
- `ForkliftInputRouter.DriverPresent`: with no driver, the forklift holds full brake and ignores the accelerator and mast levers, so the right trigger (teleport on foot) can't drive it.
- `TriggerTeleporter` (custom arc, XRI `TeleportationProvider` for the move) and `LocomotionMath` (pure: arc points, slope, step height, horizontal distance, exit speed), with 20 new EditMode test cases.
- New `Player` action map in `ForkliftControls.inputactions`: Teleport (right trigger, keyboard T), EnterForklift (A, keyboard F), ExitForklift (left stick click, keyboard F).
- Starter rig items turned off so no control has two jobs: Jump (bound to A), Climb, Grab Move, continuous turn, right-stick move, left-stick snap turn, the stick-forward teleport interactor and `ControllerInputActionManager`.

### Problems and fixes
| Problem | Cause | Fix |
|---|---|---|
| Trigger held but no teleport arc; A did nothing | `InputAction.WasPressedThisFrame()` was always false for the XR controller, while `IsPressed()` read correctly. The Input System processed the controller's events outside the frame's main input update | Both components find presses themselves by comparing `IsPressed()` with the previous frame. A trigger already held when the teleporter turns on must be released first |
| Getting out with a box beside the cab put the player on top of the box | The floor raycast hit the box top, and there was room to stand there | An exit spot must be walkable and within 0.3 m of the forklift's ground level |
| The seat recenter could pull an on-foot player into the cab | `SeatedRigRecenter` recentered at start, and disabling a component doesn't stop its coroutine | It starts disabled and is enabled only while seated, and the coroutine checks `enabled` before recentering |
| A or the trigger could do two things | The starter rig binds Jump to A, and the forklift uses A for shift-up and the trigger for the accelerator | Jump is off. The forklift map is enabled only while seated, and the teleporter only on foot. Pressing A to get in doesn't also shift gear |

### Test results (Editor Play mode, simulated XR controllers through the real Input System bindings, no headset)
- EditMode: 55/55 passed. Scripts compile with 0 errors and 0 new warnings.
- Start: on foot at forklift-space (-2.2, 0, -0.6) facing the cab, grounded, prompt showing, forklift braked.
- Left stick: back for 1 s moved exactly 2.00 m; strafe for 0.5 s moved 1.00 m, with height unchanged. Walking into the cab stopped at the chassis side (x = -0.98).
- Right stick: 90° → 135° → 90°.
- Teleport: aimed at the forklift, the arc was red with no marker, and releasing didn't move the player. Aimed at the floor, it was cyan with the marker, and the player landed 0.000 m from the target with the same heading. Spots under the chassis were rejected.
- A out of range did nothing. A in range: seated, eye error 0.000 m, locomotion and teleporter off, gear unchanged (Neutral), exit hint showing. A again while seated: Neutral → Forward.
- Driving at 2.14 m/s: no arc, and the camera stayed on the seat. An exit attempt was refused with the stop hint. After braking to 0, the exit put the player at (-1.30, 0, -0.60) on the floor.
- Left side blocked: got out at (1.30, 0, -0.60). Left, right and back blocked: stayed seated with "No room to get out here".
- On foot, holding the trigger for 1.5 s moved the forklift 0.000 m. Getting back in worked.

### Not yet tested
- **Not yet run on a Quest 3.** Room-scale walking, the real tracking-origin height, how far the arc reaches, comfort, and whether `IsPressed` presses feel right on device are unverified.
- Physical walking with the headset can still pass through walls and the forklift; only stick movement and teleport respect the body capsule.
