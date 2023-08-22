using System.Numerics;
using ImGuiNET;
using static SDL2.SDL;
using static SDL2.SDL.SDL_Keycode;
using static SDL2.SDL.SDL_EventType;
using static SDL2.SDL.SDL_WindowEventID;
using static SDL2.SDL.SDL_SystemCursor;
using static SDL2.SDL.SDL_bool;
using static SDL2.SDL.SDL_WindowFlags;

namespace ImGuiBackend
{
    public class SdlBackend
    {
        internal class ImGui_ImplSDL2_Data
        {
            internal IntPtr Window;
            //internal IntPtr Renderer;
            internal UInt64 Time;
            internal UInt32 MouseWindowID;
            internal int MouseButtonsDown;
            internal IntPtr[] MouseCursors = new IntPtr[(int)ImGuiMouseCursor.COUNT];
            internal IntPtr LastMouseCursor;
            internal int PendingMouseLeaveFrame;
            //internal string ClipboardTextData; TODO
            internal bool MouseCanUseGlobalState;
        };

        ImGui_ImplSDL2_Data bd;

        internal static ImGuiKey ImGui_ImplSDL2_KeycodeToImGuiKey(SDL_Keycode keycode)
        {
            return keycode switch
            {
                SDLK_TAB => ImGuiKey.Tab,
                SDLK_LEFT => ImGuiKey.LeftArrow,
                SDLK_RIGHT => ImGuiKey.RightArrow,
                SDLK_UP => ImGuiKey.UpArrow,
                SDLK_DOWN => ImGuiKey.DownArrow,
                SDLK_PAGEUP => ImGuiKey.PageUp,
                SDLK_PAGEDOWN => ImGuiKey.PageDown,
                SDLK_HOME => ImGuiKey.Home,
                SDLK_END => ImGuiKey.End,
                SDLK_INSERT => ImGuiKey.Insert,
                SDLK_DELETE => ImGuiKey.Delete,
                SDLK_BACKSPACE => ImGuiKey.Backspace,
                SDLK_SPACE => ImGuiKey.Space,
                SDLK_RETURN => ImGuiKey.Enter,
                SDLK_ESCAPE => ImGuiKey.Escape,
                SDLK_QUOTE => ImGuiKey.Apostrophe,
                SDLK_COMMA => ImGuiKey.Comma,
                SDLK_MINUS => ImGuiKey.Minus,
                SDLK_PERIOD => ImGuiKey.Period,
                SDLK_SLASH => ImGuiKey.Slash,
                SDLK_SEMICOLON => ImGuiKey.Semicolon,
                SDLK_EQUALS => ImGuiKey.Equal,
                SDLK_LEFTBRACKET => ImGuiKey.LeftBracket,
                SDLK_BACKSLASH => ImGuiKey.Backslash,
                SDLK_RIGHTBRACKET => ImGuiKey.RightBracket,
                SDLK_BACKQUOTE => ImGuiKey.GraveAccent,
                SDLK_CAPSLOCK => ImGuiKey.CapsLock,
                SDLK_SCROLLLOCK => ImGuiKey.ScrollLock,
                SDLK_NUMLOCKCLEAR => ImGuiKey.NumLock,
                SDLK_PRINTSCREEN => ImGuiKey.PrintScreen,
                SDLK_PAUSE => ImGuiKey.Pause,
                SDLK_KP_0 => ImGuiKey.Keypad0,
                SDLK_KP_1 => ImGuiKey.Keypad1,
                SDLK_KP_2 => ImGuiKey.Keypad2,
                SDLK_KP_3 => ImGuiKey.Keypad3,
                SDLK_KP_4 => ImGuiKey.Keypad4,
                SDLK_KP_5 => ImGuiKey.Keypad5,
                SDLK_KP_6 => ImGuiKey.Keypad6,
                SDLK_KP_7 => ImGuiKey.Keypad7,
                SDLK_KP_8 => ImGuiKey.Keypad8,
                SDLK_KP_9 => ImGuiKey.Keypad9,
                SDLK_KP_PERIOD => ImGuiKey.KeypadDecimal,
                SDLK_KP_DIVIDE => ImGuiKey.KeypadDivide,
                SDLK_KP_MULTIPLY => ImGuiKey.KeypadMultiply,
                SDLK_KP_MINUS => ImGuiKey.KeypadSubtract,
                SDLK_KP_PLUS => ImGuiKey.KeypadAdd,
                SDLK_KP_ENTER => ImGuiKey.KeypadEnter,
                SDLK_KP_EQUALS => ImGuiKey.KeypadEqual,
                SDLK_LCTRL => ImGuiKey.LeftCtrl,
                SDLK_LSHIFT => ImGuiKey.LeftShift,
                SDLK_LALT => ImGuiKey.LeftAlt,
                SDLK_LGUI => ImGuiKey.LeftSuper,
                SDLK_RCTRL => ImGuiKey.RightCtrl,
                SDLK_RSHIFT => ImGuiKey.RightShift,
                SDLK_RALT => ImGuiKey.RightAlt,
                SDLK_RGUI => ImGuiKey.RightSuper,
                SDLK_APPLICATION => ImGuiKey.Menu,
                SDLK_0 => ImGuiKey._0,
                SDLK_1 => ImGuiKey._1,
                SDLK_2 => ImGuiKey._2,
                SDLK_3 => ImGuiKey._3,
                SDLK_4 => ImGuiKey._4,
                SDLK_5 => ImGuiKey._5,
                SDLK_6 => ImGuiKey._6,
                SDLK_7 => ImGuiKey._7,
                SDLK_8 => ImGuiKey._8,
                SDLK_9 => ImGuiKey._9,
                SDLK_a => ImGuiKey.A,
                SDLK_b => ImGuiKey.B,
                SDLK_c => ImGuiKey.C,
                SDLK_d => ImGuiKey.D,
                SDLK_e => ImGuiKey.E,
                SDLK_f => ImGuiKey.F,
                SDLK_g => ImGuiKey.G,
                SDLK_h => ImGuiKey.H,
                SDLK_i => ImGuiKey.I,
                SDLK_j => ImGuiKey.J,
                SDLK_k => ImGuiKey.K,
                SDLK_l => ImGuiKey.L,
                SDLK_m => ImGuiKey.M,
                SDLK_n => ImGuiKey.N,
                SDLK_o => ImGuiKey.O,
                SDLK_p => ImGuiKey.P,
                SDLK_q => ImGuiKey.Q,
                SDLK_r => ImGuiKey.R,
                SDLK_s => ImGuiKey.S,
                SDLK_t => ImGuiKey.T,
                SDLK_u => ImGuiKey.U,
                SDLK_v => ImGuiKey.V,
                SDLK_w => ImGuiKey.W,
                SDLK_x => ImGuiKey.X,
                SDLK_y => ImGuiKey.Y,
                SDLK_z => ImGuiKey.Z,
                SDLK_F1 => ImGuiKey.F1,
                SDLK_F2 => ImGuiKey.F2,
                SDLK_F3 => ImGuiKey.F3,
                SDLK_F4 => ImGuiKey.F4,
                SDLK_F5 => ImGuiKey.F5,
                SDLK_F6 => ImGuiKey.F6,
                SDLK_F7 => ImGuiKey.F7,
                SDLK_F8 => ImGuiKey.F8,
                SDLK_F9 => ImGuiKey.F9,
                SDLK_F10 => ImGuiKey.F10,
                SDLK_F11 => ImGuiKey.F11,
                SDLK_F12 => ImGuiKey.F12,
                _ => ImGuiKey.None,
            };
        }

        internal static void ImGui_ImplSDL2_UpdateKeyModifiers(SDL_Keymod sdl_key_mods)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.AddKeyEvent(ImGuiKey.ModCtrl, (sdl_key_mods & SDL_Keymod.KMOD_CTRL) != 0);
            io.AddKeyEvent(ImGuiKey.ModShift, (sdl_key_mods & SDL_Keymod.KMOD_SHIFT) != 0);
            io.AddKeyEvent(ImGuiKey.ModAlt, (sdl_key_mods & SDL_Keymod.KMOD_ALT) != 0);
            io.AddKeyEvent(ImGuiKey.ModSuper, (sdl_key_mods & SDL_Keymod.KMOD_GUI) != 0);
        }

        public bool ImGui_ImplSDL2_ProcessEvent(SDL_Event sdlEvent)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            //ImGui_ImplSDL2_Data* bd = ImGui_ImplSDL2_GetBackendData();

            switch (sdlEvent.type)
            {
                case SDL_MOUSEMOTION:
                    {
                        Vector2 mouse_pos = new((float)sdlEvent.motion.x, (float)sdlEvent.motion.y);
                        io.AddMouseSourceEvent(sdlEvent.motion.which == SDL_TOUCH_MOUSEID ? ImGuiMouseSource.TouchScreen : ImGuiMouseSource.Mouse);
                        io.AddMousePosEvent(mouse_pos.X, mouse_pos.Y);
                        return true;
                    }
                case SDL_MOUSEWHEEL:
                    {
                        //IMGUI_DEBUG_LOG("wheel %.2f %.2f, precise %.2f %.2f\n", (float)event.wheel.x, (float)event.wheel.y, event.wheel.preciseX, event.wheel.preciseY);
                        float wheel_x = -(float)sdlEvent.wheel.x;
                        float wheel_y = (float)sdlEvent.wheel.y;
                        io.AddMouseSourceEvent(sdlEvent.wheel.which == SDL_TOUCH_MOUSEID ? ImGuiMouseSource.TouchScreen : ImGuiMouseSource.Mouse);
                        io.AddMouseWheelEvent(wheel_x, wheel_y);
                        return true;
                    }
                case SDL_MOUSEBUTTONDOWN:
                case SDL_MOUSEBUTTONUP:
                    {
                        int mouse_button = -1;
                        if (sdlEvent.button.button == SDL_BUTTON_LEFT) { mouse_button = 0; }
                        if (sdlEvent.button.button == SDL_BUTTON_RIGHT) { mouse_button = 1; }
                        if (sdlEvent.button.button == SDL_BUTTON_MIDDLE) { mouse_button = 2; }
                        if (sdlEvent.button.button == SDL_BUTTON_X1) { mouse_button = 3; }
                        if (sdlEvent.button.button == SDL_BUTTON_X2) { mouse_button = 4; }
                        if (mouse_button == -1)
                            break;
                        io.AddMouseSourceEvent(sdlEvent.button.which == SDL_TOUCH_MOUSEID ? ImGuiMouseSource.TouchScreen : ImGuiMouseSource.Mouse);
                        io.AddMouseButtonEvent(mouse_button, (sdlEvent.type == SDL_MOUSEBUTTONDOWN));
                        bd.MouseButtonsDown = (sdlEvent.type == SDL_MOUSEBUTTONDOWN) ? (bd.MouseButtonsDown | (1 << mouse_button)) : (bd.MouseButtonsDown & ~(1 << mouse_button));
                        return true;
                    }
                case SDL_TEXTINPUT:
                    {
                        unsafe
                        {
                            // TODO: Is this always 1?
                            io.AddInputCharactersUTF8(new ReadOnlySpan<char>(sdlEvent.text.text, 1));
                        }
                        return true;
                    }
                case SDL_KEYDOWN:
                case SDL_KEYUP:
                    {
                        ImGui_ImplSDL2_UpdateKeyModifiers((SDL_Keymod)sdlEvent.key.keysym.mod);
                        ImGuiKey key = ImGui_ImplSDL2_KeycodeToImGuiKey(sdlEvent.key.keysym.sym);
                        io.AddKeyEvent(key, (sdlEvent.type == SDL_KEYDOWN));
                        io.SetKeyEventNativeData(key, (int)sdlEvent.key.keysym.scancode, (int)sdlEvent.key.keysym.scancode); // To support legacy indexing (<1.87 user code). Legacy backend uses SDLK_*** as indices to IsKeyXXX() functions.
                        return true;
                    }
                case SDL_WINDOWEVENT:
                    {
                        // - When capturing mouse, SDL will send a bunch of conflicting LEAVE/ENTER event on every mouse move, but the final ENTER tends to be right.
                        // - However we won't get a correct LEAVE event for a captured window.
                        // - In some cases, when detaching a window from main viewport SDL may send SDL_WINDOWEVENT_ENTER one frame too late,
                        //   causing SDL_WINDOWEVENT_LEAVE on previous frame to interrupt drag operation by clear mouse position. This is why
                        //   we delay process the SDL_WINDOWEVENT_LEAVE events by one frame. See issue #5012 for details.
                        var window_event = sdlEvent.window.windowEvent;
                        if (window_event == SDL_WINDOWEVENT_ENTER)
                        {
                            bd.MouseWindowID = sdlEvent.window.windowID;
                            bd.PendingMouseLeaveFrame = 0;
                        }
                        if (window_event == SDL_WINDOWEVENT_LEAVE)
                            bd.PendingMouseLeaveFrame = ImGui.GetFrameCount() + 1;
                        if (window_event == SDL_WINDOWEVENT_FOCUS_GAINED)
                            io.AddFocusEvent(true);
                        else if (sdlEvent.window.windowEvent == SDL_WINDOWEVENT_FOCUS_LOST)
                            io.AddFocusEvent(false);
                        return true;
                    }
            }
            return false;
        }

        public SdlBackend(IntPtr window/*, IntPtr renderer*/)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            //IM_ASSERT(io.BackendPlatformUserData == nullptr && "Already initialized a platform backend!");

            // Check and store if we are on a SDL backend that supports global mouse position
            // ("wayland" and "rpi" don't support it, but we chose to use a white-list instead of a black-list)
            bool mouse_can_use_global_state = false;
            string sdl_backend = SDL_GetCurrentVideoDriver();
            string[] global_mouse_whitelist = new[] { "windows", "cocoa", "x11", "DIVE", "VMAN" };
            if (global_mouse_whitelist.Contains(sdl_backend))
                mouse_can_use_global_state = true;

            // Setup backend capabilities flags
            bd = new ImGui_ImplSDL2_Data()
            {
                MouseCursors = new[] {
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_ARROW),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_IBEAM),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_SIZEALL),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_SIZENS),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_SIZEWE),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_SIZENESW),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_SIZENWSE),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_HAND),
                    SDL_CreateSystemCursor(SDL_SYSTEM_CURSOR_NO),
                },
                MouseCanUseGlobalState = mouse_can_use_global_state,
                Window = window,
                //Renderer = renderer,
            };
            //io.BackendPlatformUserData = (void*)bd;
            //io.BackendPlatformName = "imgui_impl_sdl2";
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;       // We can honor GetMouseCursor() values (optional)
            io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;        // We can honor io.WantSetMousePos requests (optional, rarely used)

            //bd->Window = window;
            //bd->Renderer = renderer;

            //io.SetClipboardTextFn = ImGui_ImplSDL2_SetClipboardText;
            //io.GetClipboardTextFn = ImGui_ImplSDL2_GetClipboardText;
            //io.ClipboardUserData = nullptr;
            //io.SetPlatformImeDataFn = ImGui_ImplSDL2_SetPlatformImeData;


            // Set platform dependent data in viewport
            // Our mouse update function expect PlatformHandle to be filled for the main viewport
            ImGuiViewportPtr main_viewport = ImGui.GetMainViewport();
            // TODO
            //main_viewport->PlatformHandleRaw = nullptr;
            SDL_SysWMinfo info = new();
            SDL_VERSION(out info.version);
            if (SDL_GetWindowWMInfo(window, ref info) == SDL_bool.SDL_TRUE)
            {
                main_viewport.PlatformHandleRaw = info.info.win.window;
            }

            // From 2.0.5: Set SDL hint to receive mouse click events on window focus, otherwise SDL doesn't emit the event.
            // Without this, when clicking to gain focus, our widgets wouldn't activate even though they showed as hovered.
            // (This is unfortunately a global SDL setting, so enabling it might have a side-effect on your application.
            // It is unlikely to make a difference, but if your app absolutely needs to ignore the initial on-focus click:
            // you can ignore SDL_MOUSEBUTTONDOWN events coming right after a SDL_WINDOWEVENT_FOCUS_GAINED)
            SDL_SetHint(SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");

            // From 2.0.18: Enable native IME.
            // IMPORTANT: This is used at the time of SDL_CreateWindow() so this will only affects secondary windows, if any.
            // For the main window to be affected, your application needs to call this manually before calling SDL_CreateWindow().
            // TODO
            // SDL_SetHint(SDL_HINT_IME_SHOW_UI, "1");

            // From 2.0.22: Disable auto-capture, this is preventing drag and drop across multiple windows (see #5710)
            // TODO
            // SDL_SetHint(SDL_HINT_MOUSE_AUTO_CAPTURE, "0");
        }

        void ImGui_ImplSDL2_Shutdown()
        {
            //ImGui_ImplSDL2_Data* bd = ImGui_ImplSDL2_GetBackendData();
            //IM_ASSERT(bd != nullptr && "No platform backend to shutdown, or already shutdown?");
            ImGuiIOPtr io = ImGui.GetIO();

            //if (bd.ClipboardTextData)
            //SDL_free(bd.ClipboardTextData);
            for (int cursor_n = 0; cursor_n < (int)ImGuiMouseCursor.COUNT; cursor_n++)
                SDL_FreeCursor(bd.MouseCursors[cursor_n]);
            //bd.LastMouseCursor = null;

            //io.BackendPlatformName = nullptr;
            //io.BackendPlatformUserData = nullptr;
            //io.BackendFlags &= ~(ImGuiBackendFlags_HasMouseCursors | ImGuiBackendFlags_HasSetMousePos | ImGuiBackendFlags_HasGamepad);
            //IM_DELETE(bd);
        }

        void ImGui_ImplSDL2_UpdateMouseData()
        {
            //ImGui_ImplSDL2_Data* bd = ImGui_ImplSDL2_GetBackendData();
            ImGuiIOPtr io = ImGui.GetIO();

            // We forward mouse input when hovered or captured (via SDL_MOUSEMOTION) or when focused (below)
            // SDL_CaptureMouse() let the OS know e.g. that our imgui drag outside the SDL window boundaries shouldn't e.g. trigger other operations outside
            SDL_CaptureMouse((bd.MouseButtonsDown != 0) ? SDL_TRUE : SDL_FALSE);
            IntPtr focused_window = SDL_GetKeyboardFocus();
            bool is_app_focused = (bd.Window == focused_window);

            if (is_app_focused)
            {
                // (Optional) Set OS mouse position from Dear ImGui if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
                if (io.WantSetMousePos)
                    SDL_WarpMouseInWindow(bd.Window, (int)io.MousePos.X, (int)io.MousePos.Y);

                // (Optional) Fallback to provide mouse position when focused (SDL_MOUSEMOTION already provides this when hovered or captured)
                if (bd.MouseCanUseGlobalState && bd.MouseButtonsDown == 0)
                {
                    SDL_GetGlobalMouseState(out int mouse_x_global, out int mouse_y_global);
                    SDL_GetWindowPosition(bd.Window, out int window_x, out int window_y);
                    io.AddMousePosEvent((float)(mouse_x_global - window_x), (float)(mouse_y_global - window_y));
                }
            }
        }

        void ImGui_ImplSDL2_UpdateMouseCursor()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            if (((int)io.ConfigFlags & (int)ImGuiConfigFlags.NoMouseCursorChange) != 0)
                return;
            //ImGui_ImplSDL2_Data* bd = ImGui_ImplSDL2_GetBackendData();

            ImGuiMouseCursor imgui_cursor = ImGui.GetMouseCursor();
            if (io.MouseDrawCursor || imgui_cursor == ImGuiMouseCursor.None)
            {
                // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
                SDL_ShowCursor((int)SDL_FALSE);
            }
            else
            {
                // Show OS mouse cursor
                IntPtr expected_cursor = bd.MouseCursors[(int)imgui_cursor] != IntPtr.Zero ? bd.MouseCursors[(int)imgui_cursor] : bd.MouseCursors[(int)ImGuiMouseCursor.Arrow];
                if (bd.LastMouseCursor != expected_cursor)
                {
                    SDL_SetCursor(expected_cursor); // SDL function doesn't have an early out (see #6113)
                    bd.LastMouseCursor = expected_cursor;
                }
                SDL_ShowCursor((int)SDL_TRUE);
            }
        }

        void ImGui_ImplSDL2_UpdateGamepads()
        {
            throw new NotImplementedException("Nah");
        }

        public void ImGui_ImplSDL2_NewFrame()
        {
            //ImGui_ImplSDL2_Data* bd = ImGui_ImplSDL2_GetBackendData();
            //IM_ASSERT(bd != nullptr && "Did you call ImGui_ImplSDL2_Init()?");
            ImGuiIOPtr io = ImGui.GetIO();

            // Setup display size (every frame to accommodate for window resizing)
            SDL_GetWindowSize(bd.Window, out int w, out int h);
            if ((SDL_GetWindowFlags(bd.Window) & (int)SDL_WINDOW_MINIMIZED) != 0)
            {
                w = 0;
                h = 0;
            }
            int display_w;
            int display_h;
            // if (bd.Renderer != IntPtr.Zero)
            //     SDL_GetRendererOutputSize(bd.Renderer, out display_w, out display_h);
            // else
            SDL_GL_GetDrawableSize(bd.Window, out display_w, out display_h);
            io.DisplaySize = new Vector2((float)w, (float)h);
            if (w > 0 && h > 0)
                io.DisplayFramebufferScale = new Vector2((float)display_w / w, (float)display_h / h);

            // Setup time step (we don't use SDL_GetTicks() because it is using millisecond resolution)
            // (Accept SDL_GetPerformanceCounter() not returning a monotonically increasing value. Happens in VMs and Emscripten, see #6189, #6114, #3644)
            UInt64 frequency = SDL_GetPerformanceFrequency();
            UInt64 current_time = SDL_GetPerformanceCounter();
            if (current_time <= bd.Time)
                current_time = bd.Time + 1;
            io.DeltaTime = bd.Time > 0 ? (float)((double)(current_time - bd.Time) / frequency) : (float)(1.0f / 60.0f);
            bd.Time = current_time;

            if (bd.PendingMouseLeaveFrame > 0 && bd.PendingMouseLeaveFrame >= ImGui.GetFrameCount() && bd.MouseButtonsDown == 0)
            {
                bd.MouseWindowID = 0;
                bd.PendingMouseLeaveFrame = 0;
                io.AddMousePosEvent(float.MinValue, float.MinValue);
            }

            ImGui_ImplSDL2_UpdateMouseData();
            ImGui_ImplSDL2_UpdateMouseCursor();

            // Update game controllers (if enabled and available)
            //ImGui_ImplSDL2_UpdateGamepads();
        }
    }
}
