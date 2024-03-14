using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pico4SAFTExtTrackingModule.PicoConnectors.ProgramChecker;

public interface IProgramChecker
{
    public bool Check(PicoPrograms program);
}