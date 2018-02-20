using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using OpenCL.Net.Extensions;
using OpenCL.Net;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DNWS
{
  class GPUPlugin : IPlugin
  {
    private Context _context;
    private Device _device;
    public GPUPlugin()
    {
      ErrorCode error;

      Platform[] platforms = Cl.GetPlatformIDs(out error);
      List<Device> devicesList = new List<Device>();

      LogError (error, "Cl.GetPlaformIDs");

      foreach (Platform platform in platforms) {
        string platformName = Cl.GetPlatformInfo(platform, PlatformInfo.Name, out error).ToString();
        Console.WriteLine("Platform: " + platformName);
        LogError (error, "Cl.GetPlatformInfo");
        foreach (Device device in Cl.GetDeviceIDs(platform, DeviceType.Gpu, out error)) {
          LogError(error, "Cl.GetDeviceIDs");
          Console.WriteLine("Device:" + device.ToString() );
          devicesList.Add(device);
        }
      }

      if(devicesList.Count <= 0) {
        Console.WriteLine("No devices found.");
        return;
      }

      _device = devicesList[0];
      if(Cl.GetDeviceInfo(_device, DeviceInfo.ImageSupport, out error).CastTo<OpenCL.Net.Bool>() == OpenCL.Net.Bool.False)
      {
        Console.WriteLine("No image support.");
        return;
      }
      _context = Cl.CreateContext(null, 1, new[] {_device}, ContextNotify, IntPtr.Zero, out error);
      LogError(error, "Cl.CreateContext");

    }
    private void ContextNotify(string errInfo, byte[] data, IntPtr cb, IntPtr userData) {
        Console.WriteLine("OpenCL Notification: " + errInfo);
    }
    public void PreProcessing(HTTPRequest request)
    {
      throw new NotImplementedException();
    }
    private void LogError(ErrorCode err, string name)
    {
      if (err != ErrorCode.Success) {
          Console.WriteLine("ERROR: " + name + " (" + err.ToString() + ")");
      }
    }
    public HTTPResponse GetResponse(HTTPRequest request)
    {
      HTTPResponse response = new HTTPResponse(200);
      StringBuilder sb = new StringBuilder();
      ErrorCode error;

      string programPath = System.Environment.CurrentDirectory +  "/Kernel.cl";
      if(!System.IO.File.Exists(programPath)) {
        Console.WriteLine("Program doesn't exist at path " + programPath);
        return new HTTPResponse(404);
      }

      sb.Append("<html><body>");
      string programSource = System.IO.File.ReadAllText(programPath);
      using (OpenCL.Net.Program program = Cl.CreateProgramWithSource(_context, 1, new[] {programSource}, null, out error)) {
        LogError(error, "Cl.CreateProgramWithSource");
        error = Cl.BuildProgram(program, 1, new[] {_device}, string.Empty, null, IntPtr.Zero);
        LogError(error, "Cl.BuildProgram");
        if (Cl.GetProgramBuildInfo (program, _device, ProgramBuildInfo.Status, out error).CastTo<OpenCL.Net.BuildStatus>()
            != BuildStatus.Success) {
            LogError(error, "Cl.GetProgramBuildInfo");
            Console.WriteLine("Cl.GetProgramBuildInfo != Success");
            Console.WriteLine(Cl.GetProgramBuildInfo(program, _device, ProgramBuildInfo.Log, out error));
            return new HTTPResponse(404);
        }
        Kernel kernel = Cl.CreateKernel(program, "answer", out error);
        LogError(error, "Cl.CreateKernel");

        Random rand = new Random();
        int[] input = (from i in Enumerable.Range(0, 100) select (int)rand.Next()).ToArray();
        int[] output = new int[100];

        var buffIn = _context.CreateBuffer(input, MemFlags.ReadOnly);
        var buffOut = _context.CreateBuffer(output, MemFlags.WriteOnly);
        int IntPtrSize = Marshal.SizeOf(typeof(IntPtr)); 
        error = Cl.SetKernelArg(kernel, 0, (IntPtr)IntPtrSize, buffIn);
        error |= Cl.SetKernelArg(kernel, 1, (IntPtr)IntPtrSize, buffOut);
        LogError(error, "Cl.SetKernelArg");
        CommandQueue cmdQueue = Cl.CreateCommandQueue(_context, _device, (CommandQueueProperties)0, out error);
        LogError(error, "Cl.CreateCommandQueue");
        Event clevent;
        error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 2, null, new[]{(IntPtr)100,(IntPtr)1}, null, 0, null, out clevent);
        LogError(error, "Cl.EnqueueNDRangeKernel");
        error = Cl.Finish(cmdQueue);
        LogError(error, "Cl.Finih");
        error = Cl.EnqueueReadBuffer(cmdQueue, buffOut, OpenCL.Net.Bool.True, 0, 100, output, 0, null, out clevent);
        LogError(error, "Cl.EnqueueReadBuffer");
        error = Cl.Finish(cmdQueue);
        LogError(error, "Cl.Finih");


        Cl.ReleaseKernel(kernel);
        Cl.ReleaseCommandQueue(cmdQueue);
        Cl.ReleaseMemObject(buffIn);
        Cl.ReleaseMemObject(buffOut);
        sb.Append("<pre>");
        for(int i = 0; i != 100; i++) {
          sb.Append(input[i] + " % 42 = " + output[i] + "<br />");
        }
        sb.Append("</pre>");
      }  
      sb.Append("</body></html>");
      response.body = Encoding.UTF8.GetBytes(sb.ToString());
      return response;
    }

    public HTTPResponse PostProcessing(HTTPResponse response)
    {
      throw new NotImplementedException();
    }
  }
}