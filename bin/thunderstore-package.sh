#!/usr/bin/env bash

set -euo pipefail

SS_VERSION=$(grep -oP '(?<=\[BepInPlugin\(PluginGuid, "Dedicated Simulation", ")\d+\.\d+\.\d+(?="\)\]\r?$)' src/Valheim_Serverside/ServersidePlugin.cs)

cat > manifest.json <<- EOM
{
  "name": "DedicatedSimulation",
  "description": "Run world and monster simulations on a dedicated server.",
  "version_number": "$SS_VERSION",
  "dependencies": ["denikson-BepInExPack_Valheim-5.4.2202"],
  "website_url": "https://github.com/liekos47/valheim-dedicated-simulation"
}
EOM

zip thunderstore-package.zip DedicatedSimulation.dll icon.png manifest.json README.md CHANGELOG.md
