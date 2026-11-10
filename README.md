# Pluto Flight Academy

A Unity-based drone flight simulation project with an AR scene,
on-screen flight controls, a guided tutorial, and a checkpoint course.

> **Project status:** The Unity project source is available in this
> repository. The screenshots below show the current app interface and
> example simulation states.

## Screenshots and simulation states

### 1. Disarmed state

![Pluto Flight Academy --- disarmed
state](docs/images/01-disarmed-state.png)

The drone is placed in the AR environment but is **not armed**. The
status panel shows `Disarmed`, altitude `0.00 m`, and speed `0.00 m/s`.
The on-screen controls include the left and right control sticks, an
altitude slider, and the Arm, Disarm, Take Off, and Land buttons.

### 2. Hovering state

![Pluto Flight Academy --- hovering
state](docs/images/02-hovering-state.png)

The drone has taken off and is maintaining a hover. In this example, the
status panel shows an altitude of approximately **1.03 m** and a speed
of **0.00 m/s**. This demonstrates the simulation's flight-state and
altitude display.

### 3. Checkpoint course

![Pluto Flight Academy --- checkpoint
course](docs/images/03-checkpoint-course.png)

The checkpoint challenge is running. The **magenta rings** are visual
course targets for the drone to pass through. The HUD displays the
number of rings collected (`0 / 10` in this screenshot), elapsed time,
and missed checkpoints.

## On-screen controls

  -----------------------------------------------------------------------
  Control                             Meaning
  ----------------------------------- -----------------------------------
  Left stick                          Controls throttle and yaw.

  Right stick                         Controls pitch and roll.

  ALT slider                          Adjusts the altitude/throttle input
                                      shown by the interface.

  ARM                                 Arms the drone for flight.

  DISARM                              Disarms the drone.

  TAKE OFF                            Starts the take-off action.

  LAND                                Starts the landing action.

  START CHECKPOINTS                   Starts the checkpoint course. The
                                      button changes to **STOP
                                      CHECKPOINTS** while the course is
                                      active.

  RESET                               Resets the simulation/interface
                                      state.
  -----------------------------------------------------------------------

## Status display

-   **State:** The current flight state, such as `Disarmed` or
    `Hovering`.
-   **Altitude:** The drone's displayed height in metres.
-   **SPD:** The displayed speed in metres per second.
-   **RINGS:** Checkpoints collected out of the total course rings.
-   **TIME:** Time elapsed during the checkpoint challenge.
-   **MISS:** Number of missed checkpoint targets.

## Project structure

``` text
pluto-flight-academy/
├── Assets/
│   ├── Models/
│   ├── Prefabs/
│   ├── Scenes/
│   └── Scripts/
├── Packages/
├── ProjectSettings/
├── docs/
│   └── images/
│       ├── 01-disarmed-state.png
│       ├── 02-hovering-state.png
│       └── 03-checkpoint-course.png
└── README.md
```

## Open the Unity project

1.  Download or clone this repository.
2.  Open **Unity Hub**.
3.  Select **Add** (or **Add project from disk**).
4.  Choose the downloaded `pluto-flight-academy` folder.
5.  Open it with a compatible Unity 6.6 editor version.
6.  Allow Unity to import the project and resolve its packages.
7.  Open the project scene from `Assets/Scenes/` and press Play.

## Notes

-   `Assets`, `Packages`, and `ProjectSettings` are the main Unity
    project folders tracked in this repository.
-   Unity may regenerate local folders such as `Library`, `Temp`, and
    `Logs` when the project is opened. These generated folders do not
    need to be committed.
-   The screenshots document the interface and example states; they are
    not a substitute for the Unity project files.
-   An Android APK is a separate installable build. If one is published,
    add it to the repository's **Releases** section so users can
    download the app without opening Unity.

## Team members

- **Jatin Lalchandani**
- **Devyani Kolhe**
- **Wilson Kadam**

## Credits

Developed by the team as the **Pluto Flight Academy** Unity drone
simulation project.
