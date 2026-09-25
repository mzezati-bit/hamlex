import pathlib
import sys

project_dir = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
source = (project_dir.parent / "InvoicePrintPage.xaml.cs").read_text(encoding="utf-8")
marker = "internal static class InvoicePagePlanner"
start = source.index(marker)
brace = source.index("{", start)
depth = 0
end = None
for index in range(brace, len(source)):
    char = source[index]
    if char == "{":
        depth += 1
    elif char == "}":
        depth -= 1
        if depth == 0:
            end = index + 1
            break
if end is None:
    raise SystemExit("InvoicePagePlanner class was not found")

body = source[start:end]
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(
    "using System;\n"
    "using System.Collections.Generic;\n"
    "\n"
    "namespace hamlex.Views.Prints\n"
    "{\n"
    + body
    + "\n}\n",
    encoding="utf-8",
)
