using System.Diagnostics;

namespace _3dtest;

class Program
{
    // Camera state
    static double camx = 0.0;
    static double camz = 0.0;

    // Scene points (x, y, z)
    static readonly (double x, double y, double z)[] points = new (double, double, double)[]
    {
        (-1.5, -0.5, 5),
        (-1.5,  0.5, 5),
        (-0.5,  0.5, 5),
        (-0.5, -0.5, 5),
        (-1.5, -0.5, 4),
        (-1.5,  0.5, 4),
        (-0.5,  0.5, 4),
        (-0.5, -0.5, 4),
    };

    // Virtual render target and projection constants to mimic the Lua snippet
    const int VWidth = 480;
    const int VHeight = 270;
    const double CX = VWidth / 2.0;   // 240
    const double CY = VHeight / 2.0;  // 135
    const double FX = 270.0;          // from sx = (x/z) * 270 + 240
    const double FY = 135.0;          // from sy = (y/z) * 135 + 135

    static void Main(string[] args)
    {
        // Improve output performance on some terminals
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}
        Console.WriteLine("Press W/A/S/D to move. Esc to quit.");
        Console.CursorVisible = false;
        try
        {
            bool running = true;

            // Fixed 60 Hz loop using Stopwatch for high-resolution timing
            var stopwatch = Stopwatch.StartNew();
            TimeSpan targetFrameTime = TimeSpan.FromSeconds(1.0 / 60.0);
            TimeSpan nextTick = stopwatch.Elapsed; // schedule first tick immediately

            while (running)
            {
                var now = stopwatch.Elapsed;
                if (now >= nextTick)
                {
                    // Process all pending input this frame (non-blocking)
                    while (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);

                        // Update camera like the Lua sample
                        const double step = 0.05;
                        if (keyInfo.Key == ConsoleKey.W) { camz -= step; }
                        if (keyInfo.Key == ConsoleKey.S) { camz += step; }
                        if (keyInfo.Key == ConsoleKey.A) { camx -= step; }
                        if (keyInfo.Key == ConsoleKey.D) { camx += step; }

                        // Exit condition
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                            running = false;
                        }
                    }

                    // Update camera based on inputs captured this frame (WASD)
                    // Mapping to Lua: A(D)=camx -/+, W(S)=camz -/+
                    // Note: Key repeat is controlled by the OS; this approximates btn() polling.
                    // (Already handled above when dequeuing keys)

                    // Render the scene to console
                    RenderScene();
                    // This block runs 60 times per second (as closely as the OS allows).

                    // Schedule next tick and avoid drift
                    nextTick += targetFrameTime;

                    // If we've fallen far behind (e.g., long GC pause), skip ahead to avoid spiral of death
                    if (now - nextTick > TimeSpan.FromSeconds(1))
                    {
                        nextTick = now + targetFrameTime;
                    }
                }
                else
                {
                    // Sleep most of the remaining time to save CPU, then spin briefly for precision
                    var sleepFor = nextTick - now;
                    if (sleepFor > TimeSpan.FromMilliseconds(2))
                    {
                        System.Threading.Thread.Sleep(sleepFor - TimeSpan.FromMilliseconds(1));
                    }
                    else if (sleepFor > TimeSpan.Zero)
                    {
                        // Short spin-wait for the last millisecond or so
                        var sw = Stopwatch.StartNew();
                        while (sw.Elapsed < sleepFor) { }
                    }
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.WriteLine("Goodbye!");
        }
    }

    static void RenderScene()
    {
        // Determine console target size
        int cols = Math.Max(40, Console.WindowWidth);
        int rows = Math.Max(12, Console.WindowHeight);

        // We'll use all rows but keep the last line to avoid accidental scroll
        rows = Math.Max(12, rows - 1);

        // Prepare framebuffer
        var fb = new char[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                fb[r, c] = ' ';

        // Optional HUD line at top
        string hud = $"W/S: camZ +/- | A/D: camX -/+   camx={camx:F2} camz={camz:F2}   Esc: quit";
        for (int i = 0; i < Math.Min(hud.Length, cols); i++) fb[0, i] = hud[i];

        // Project and plot points
        foreach (var p in points)
        {
            double x = p.x - camx;
            double y = p.y;         // no camy in sample
            double z = p.z + camz;

            if (z > 1.0)
            {
                double sx = (x / z) * FX + CX; // 0..VWidth
                double sy = (y / z) * FY + CY; // 0..VHeight

                if (sx >= 0 && sx < VWidth && sy >= 0 && sy < VHeight)
                {
                    // Map virtual 480x270 to console cols x rows
                    int c = (int)Math.Round(sx / (VWidth - 1) * (cols - 1));
                    int r = (int)Math.Round(sy / (VHeight - 1) * (rows - 1));
                    // Plot a dot (try a bullet if supported)
                    fb[r, c] = '•';
                }
            }
        }

        // Blit framebuffer
        var sb = new System.Text.StringBuilder(rows * (cols + 1));
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++) sb.Append(fb[r, c]);
            if (r < rows - 1) sb.Append('\n');
        }

        try { Console.SetCursorPosition(0, 0); } catch { }
        Console.Write(sb.ToString());
    }
}