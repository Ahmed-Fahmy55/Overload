using System;
using System.Collections.Generic;

namespace Deadball.Fighters
{
    /// <summary>
    /// Where the match gets its two fighters from.
    /// </summary>
    /// <remarks>
    /// Local Versus fills this from a join screen; Solo will fill it with one human and one AI. The
    /// match director does not care which, and the design is explicit that the only difference
    /// between the two modes is what drives player two (GDD section 11). Keeping the roster behind
    /// an interface is what makes that literally true instead of aspirational.
    /// </remarks>
    public interface IFighterRoster
    {
        IReadOnlyList<Fighter> Fighters { get; }

        /// <summary>True once every slot the mode needs has been claimed.</summary>
        bool IsReady { get; }

        /// <summary>Raised the moment the roster becomes ready.</summary>
        event Action RosterComplete;
    }
}
