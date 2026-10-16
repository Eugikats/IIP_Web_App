#!/usr/bin/env bash
# Like run-offline.sh, plus the local stand-in payment provider, so the buying flow can be tried and tested on this machine.
# The stand-in is only honoured in Development auth mode. It is never used in a deployed environment.
export Academy__Payments__Provider=Fake
export Academy__PortalBaseUrl=http://localhost:3000
exec "$(dirname "$0")/run-offline.sh"
