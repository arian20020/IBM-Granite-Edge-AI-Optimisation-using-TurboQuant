# Risk, Assumption, Constraint and Licence Register

| ID | Type | Description | Probability | Impact | Validation or trigger | Mitigation | Contingency | Owner | Status | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| R-001 | Schedule | Development may exceed 15 August | Medium | High | Weekly review | Protect Must Haves | Defer Experimental and Should work | Arian B | Open | Timetable |
| R-002 | Technical | TurboQuant may fail or silently fall back | High | High | Activation/backend checks | Keep upstream primary | Remove Experimental option | Arian B | Open | Test workbooks |
| A-001 | Assumption | Raw experiment evidence can be recovered | Medium | High | Evidence audit | Recover and checksum now | Repeat minimum critical runs | Arian B | Open | Evidence manifest |
