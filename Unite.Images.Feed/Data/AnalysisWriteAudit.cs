namespace Unite.Images.Feed.Data;

public class AnalysisWriteAudit
{
    public int RadFeaturesCreated;
    public int RadFeaturesAssociated;

    public HashSet<int> Images = [];

    public override string ToString()
    {
        return string.Join(Environment.NewLine,[
            $"{RadFeaturesCreated} radiomics features created",
            $"{RadFeaturesAssociated} rediomics features associated"
        ]);
    }
}
