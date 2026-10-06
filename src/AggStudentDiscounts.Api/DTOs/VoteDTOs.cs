namespace AggStudentDiscounts.Api.DTOs;

public class VoteRequest
{
    public string Vote { get; set; }  = string.Empty; 
}

public class VoteStatsResponse
{
    public int Yes { get; set; }
    public int No { get; set; }
    public DateTime? LastVoteDateYes { get; set; }
    public DateTime? LastVoteDateNo { get; set; }
    public bool CanVoteAgain { get; set; }
}