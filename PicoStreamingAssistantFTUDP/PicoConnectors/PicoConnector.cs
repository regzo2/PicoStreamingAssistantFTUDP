using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors;

public interface PicoConnector
{
    string GetProcessName();

    bool Connect();

    unsafe float* GetBlendShapes();

    void Teardown();
}