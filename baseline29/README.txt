Same-host baseline, Linux server (Xvfb + llvmpipe software GL), Unity 6000.3.25f1, full EditMode.
Main baseline: 5d1b54abf07dc542dce175fb36ca4235e78b60f7 (3540 total, 3500 passed, 26 failed, 14 skipped), run on current main.
Head runs were done on each PR's own merge base earlier (not rebased onto this main). Compared by test full name.
Heads: #1957 db6b53c3 (full run on the commit before the last test-isolation delta), #1961 73ebbd3b (later head has strings/uss-only commits),
#1964 81fa1f45, #1966 4232d83c, #1967 26d92435, #1923 f8169df7, mh = laneMH worktree (no PR yet, base 036fae62).
