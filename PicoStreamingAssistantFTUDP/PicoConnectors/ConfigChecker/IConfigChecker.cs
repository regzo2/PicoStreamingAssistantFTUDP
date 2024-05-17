using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker;

public interface IConfigChecker
{
    int GetTransferProtocolNumber(PicoPrograms program);
}