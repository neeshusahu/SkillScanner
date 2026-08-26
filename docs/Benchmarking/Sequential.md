
# Sequential LLM Benchmark

## Total LLM Calls and Time

| Metric | Value |
| --- | ---: |
| Total LLM calls made | **110** |
| Completed calls | **108** |
| Timed-out calls | **2** — both in `Skill.md` |
| Total LLM time | **1,281.73s** (**21m 21.7s**) |
| Wall-clock time | **21:48:03 to 22:09:25** (**21m 22s**) |
| Average time per call | **11.65s** |
| Fastest call | **1.61s** (`azure-key-vault.md`) |
| Slowest completed call | **19.86s** (`Skill.md`) |
| Timeout durations | **20.037s** and **20.014s** |

The calls ran effectively serially: the total LLM duration is only about 0.24 seconds below the total wall-clock duration.

## Per-File Breakdown

| File | Calls | LLM Time | Average per Call |
| --- | ---: | ---: | ---: |
| `File_01.md` | 10 | 70.28s | 7.03s |
| `File_02.md` | 10 | 98.93s | 9.89s |
| `File_03.md` | 10 | 101.92s | 10.19s |
| `File_04.md` | 10 | 112.05s | 11.21s |
| `File_05.md` | 10 (2 timeouts) | 164.86s | 16.49s |
| `File_06.md` | 10 | 125.37s | 12.54s |
| `File_07.md` | 10 | 115.68s | 11.57s |
| `File_08.md` | 10 | 119.60s | 11.96s |
| `File_09.md` | 10 | 123.32s | 12.33s |
| `aFile_10.md` | 10 | 121.66s | 12.17s |
| `File_11.md` | 10 | 128.06s | 12.81s |
| **Total** | **110** | **1,281.73s** | **11.65s** |

## Results

- Flagged responses: **40**
- Unflagged responses: **68**
- Failed or timed-out requests: **2**
