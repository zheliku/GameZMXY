"""Run native GGF shutdown regressions against a loopback-only HTTP server."""

import argparse
import http.server
import os
from pathlib import Path
import subprocess
import threading


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2] / "Godot/GodotProject"
    output = args.output.resolve()
    output.relative_to(project / ".godot/validation")
    output.mkdir(parents=True, exist_ok=True)
    stopped = threading.Event()

    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path.startswith("/slow/"):
                stopped.wait(30)
            self.respond(b"get")

        def do_POST(self):
            self.respond(self.rfile.read(int(self.headers["Content-Length"])))

        def respond(self, body):
            try:
                self.send_response(200)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except OSError:
                pass  # Cancellation and shutdown intentionally close slow requests.

        def log_message(self, *_args):
            pass

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    server.daemon_threads = True
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    origin = f"http://127.0.0.1:{server.server_port}"
    driver = output / "shutdown_driver.gd"
    driver.write_text(
        '''extends SceneTree

func _initialize() -> void:
    call_deferred("_run")

func _wait_for(probe, method: String) -> void:
    for frame in range(600):
        if probe.call(method):
            return
        await process_frame
    push_error("SHUTDOWN FAIL: timeout waiting for " + method)
    quit(1)

func _run() -> void:
    var probe = load("res://EditorScripts/ValidateFrameworkShutdown.cs").new()
    for explicit in [false, true, false]:
        var framework = probe.CreateFramework()
        root.add_child(framework)
        probe.StartNormal("ORIGIN")
        await _wait_for(probe, "RequestsCompleted")
        probe.VerifyNormal()
        probe.StartTimeout("ORIGIN")
        await _wait_for(probe, "RequestsCompleted")
        probe.VerifyTimeout()
        probe.StartNormal("ORIGIN")
        await _wait_for(probe, "RequestsCompleted")
        probe.VerifyNormal()
        probe.StartPending("ORIGIN")
        await _wait_for(probe, "RequestsRunning")
        if explicit:
            probe.StopExplicitly()
        framework.queue_free()
        await process_frame
        await process_frame
        probe.VerifyStopped()
        print("SHUTDOWN-CYCLE PASS: explicit=", explicit)
    var final_framework = probe.CreateFramework()
    root.add_child(final_framework)
    probe.StartNormal("ORIGIN")
    await _wait_for(probe, "RequestsCompleted")
    probe.VerifyNormal()
    probe.StartPending("ORIGIN")
    await _wait_for(probe, "RequestsRunning")
    print("SHUTDOWN PASS: GET/POST, timeout/reuse, pending/queued requests, repeated shutdown and recreation; final native quit with live requests.")
    quit()
'''.replace("ORIGIN", origin),
        encoding="utf-8",
    )
    appdata = output / "appdata"
    appdata.mkdir(exist_ok=True)
    command = [str(args.godot), "--path", str(project), "--headless", "--audio-driver", "Dummy",
               "--verbose", "--script", "res://" + driver.relative_to(project).as_posix()]
    try:
        with (output / "stdout.log").open("w", encoding="utf-8") as out, (output / "stderr.log").open("w", encoding="utf-8") as err:
            result = subprocess.run(command, env=dict(os.environ, APPDATA=str(appdata)), stdout=out,
                                    stderr=err, timeout=60, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    finally:
        stopped.set()
        server.shutdown()
        server.server_close()
    stdout = (output / "stdout.log").read_text(encoding="utf-8")
    stderr = (output / "stderr.log").read_text(encoding="utf-8")
    print("\n".join(line for line in stdout.splitlines() if "SHUTDOWN" in line))
    print("\n".join(line for line in stderr.splitlines() if line.startswith(("ERROR:", "WARNING:"))))
    if result.returncode != 0 or "SHUTDOWN PASS:" not in stdout or "ERROR:" in stderr or "leaked" in stderr:
        raise SystemExit(f"SHUTDOWN FAIL: exit={result.returncode}; inspect {output}")
    print("FRAMEWORK SHUTDOWN PASS: no exception or leaked object/resource diagnostics.")


if __name__ == "__main__":
    main()
