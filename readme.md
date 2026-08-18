# unity tum main campus

## About this copy of the project

This is a standalone copy of [TUM-VT/Sumonity-UnityBaseProject](https://github.com/TUM-VT/Sumonity-UnityBaseProject) with VR cycling experiment work added on top. It is not a GitHub fork, but the upstream history is preserved, so the branches line up like this:

- `main` and `dev-jo` are unmodified copies of the TUM-VT baseline
- `dev-param` is the default branch and holds all of the added work

To review only what was added, use `git diff dev-jo..dev-param`.

Design notes, scenario specs and a macOS setup guide live in `docs/`; start with `docs/PROJECT_MINDMAP.md`.

## Quick start: download and play

Everything needed to run the simulator in the Unity Editor is committed here, including the campus 3D models and all the dependency packages. There is no `vcs import` step and no separate model download.

1. Get the project, either way works:
   - **Download ZIP**: use the green **Code** button above, then **Download ZIP**, and unpack it.
   - **Clone**: `git lfs install` first, then `git clone https://github.com/padfoot33/tum-vr-cycling-simulator.git`. Without Git LFS installed, the large files arrive as small text pointers.
2. Open the folder in Unity **6000.4.2f1** (the version pinned in `ProjectSettings/ProjectVersion.txt`).
3. Wait for the first import. It processes about 1.4 GB of assets and can take a long time. `Library/` is deliberately not shipped because it is large and not portable between machines.
4. Open `Assets/Scenes/MainScene.unity` and press Play.

The only thing not included is the Python virtual environment for the SUMO bridge. Driving live SUMO traffic additionally needs SUMO 1.21 and Python 3.11, set up as described under Manual installation below. The scene itself opens and plays without them.
x   
## Prerequistes
- Sumo 1.21 (or later, important traci must be the same version)
- Windows 10/11
- Python 3.11 
- Git Bash
- Unity 6000.4.2f1 (the version recorded in `ProjectSettings/ProjectVersion.txt`)

## Automatic installation

Clone the repo:
```
git lfs install
git clone https://github.com/padfoot33/tum-vr-cycling-simulator.git
```

Execute the install script in Powershell as Admin:

```
cd .\tum-vr-cycling-simulator\
.\setup.ps1
```

The `download_unity_fbx.ps1` and `vcs import` steps that this script performs are no longer required: the campus model and all dependency packages are committed to this repository. They are kept for the upstream workflow, and for pulling fresh versions from TUM.

## Manual installation



### Repo Setup

Make sure to have the ssh key of this machine your are working on added to your account.

Use Git Bash for the setup of the repo, otherwise vcs tools will not work

install vcs tools:
```
pip install vcstool2
```

IMPORTANT: Check for warnings regarding the PATH variable. 

get submodules
```
vcs import < assets.repos

```

download the 3d model:
```
wget "https://gitlab.lrz.de/tum-gis/tum2twin-datasets/-/raw/0ec6f8d87cfe58ac03bdae2c690632c08fd3d625/fbx/tum_main_campus.fbx" -OutFile "Assets/3d_model/tum_main_campus.fbx"
```



### Sumo Python Envrionment Setup

The prompts in this guide refer to git bash and not "powershell" or "cmd"

Go to The Sumo Folder where the python script for "TraCI" is located:

Setup the envrionment
```
cd Assets/Sumonity/SumoTraCI
```

Install the virtualenvironment toolset
```
pip install virtualenv 
```

Enable execution of scripts, open powershell in admin mode:
```
Set-ExecutionPolicy Unrestricted
```


Activate it and install dependencies:
```
python3.11 -m venv venv
.\venv\scripts\activate
pip install -r requirements.txt

```

Note: 
- Use Python 3.11, otherwise you will run into compatability issues.


## Running the Simulation

Open the project in Unity and run `Assets/Scenes/MainScene.unity`.

## Troubleshooting

### VCS Tool and Win11
If you have to work with Windows 11, install vcstool2 in a python venv!
