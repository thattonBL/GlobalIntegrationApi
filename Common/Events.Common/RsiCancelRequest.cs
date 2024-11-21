using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Events.Common;

public record RsiCancelRequest
{
    public string Identifier { get; set; }
}
