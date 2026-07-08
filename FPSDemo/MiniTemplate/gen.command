#!/bin/bash
cd "$(dirname "$0")" || exit 1
./gen.sh
echo
read -n 1 -s -r -p "Press any key to close..."
echo
