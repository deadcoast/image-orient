namespace OrientExpress.Spike002;

/// <summary>Everything the detector saw for one restore candidate of one photo.</summary>
public sealed record CandidateEvidence(
    float TopScore,
    float SumScore,
    int FaceCount,
    bool TopFaceEyesAboveMouth);

/// <summary>The outcome of a decision rule on one photo presentation.</summary>
public sealed record RuleDecision(int Chosen, float Margin, bool Abstained, string? AbstainReason = null);

/// <summary>
/// Candidate decision rules under evaluation (SPIKE-002). Rule A is the
/// naive single-best-face score. Rule B uses every face in the photo (group
/// shots vote) and the eyes-above-mouth landmark geometry as an independent
/// check: it abstains when geometry disagrees with the score winner or when
/// the winner is not clearly separated. The evaluator reports both so rule
/// changes are measured, never asserted.
/// </summary>
public static class DecisionRules
{
    /// <summary>Rule A: argmax of the single best face score; margin = top1 − top2.</summary>
    public static RuleDecision RuleA(IReadOnlyList<CandidateEvidence> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var scores = candidates.Select(c => c.TopScore).ToArray();
        var chosen = IndexOfMax(scores);
        return new RuleDecision(chosen, Margin(scores, chosen), Abstained: false);
    }

    /// <summary>
    /// Rule B: argmax of the summed face scores; abstains unless the winner's
    /// top-face geometry is upright and the winner is separated by a relative
    /// margin. The opposite-rotation geometry is deliberately NOT a veto: the
    /// detector hallucinates upright-looking landmarks on inverted faces, so
    /// that signal is unreliable by construction.
    /// </summary>
    public static RuleDecision RuleB(IReadOnlyList<CandidateEvidence> candidates, float relativeMargin = 0.08f)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var scores = candidates.Select(c => c.SumScore).ToArray();
        var chosen = IndexOfMax(scores);
        var margin = Margin(scores, chosen);

        var geometryAgrees = candidates[chosen].TopFaceEyesAboveMouth;
        var separated = scores[chosen] > 0
            && margin / Math.Max(scores[chosen], 1e-6f) >= relativeMargin;

        var reason = !geometryAgrees ? "geometry" : !separated ? "separation" : null;
        return new RuleDecision(chosen, margin, reason is not null, reason);
    }

    private static int IndexOfMax(float[] values)
    {
        var best = 0;
        for (var index = 1; index < values.Length; index++)
        {
            if (values[index] > values[best])
            {
                best = index;
            }
        }

        return best;
    }

    private static float Margin(float[] values, int chosen)
    {
        var second = float.MinValue;
        for (var index = 0; index < values.Length; index++)
        {
            if (index != chosen && values[index] > second)
            {
                second = values[index];
            }
        }

        return values[chosen] - (second == float.MinValue ? 0 : second);
    }
}
