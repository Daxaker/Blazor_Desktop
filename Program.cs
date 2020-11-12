using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Blazor_Desktop.Pages;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpDX.Text;
using WebWindows.Blazor;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using WebWindows;
using Encoding = System.Text.Encoding;

namespace Blazor_Desktop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ComponentsDesktop.Run<Startup>("AppWnd", "wwwroot/index.html");
            
            //Render();
        }
    }
}
