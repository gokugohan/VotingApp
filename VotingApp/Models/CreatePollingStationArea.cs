using System.ComponentModel.DataAnnotations;

namespace VotingApp.Models
{
    public class CreatePollingStationArea
    {

        [Required]
        [Display(Name ="Suco/Village")]
        public string AreaId { get; set; }

        [Display(Name ="Polling Station Name")]
        public string StationName { get; set; }
    }
}
