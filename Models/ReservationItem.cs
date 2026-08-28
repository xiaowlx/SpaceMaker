namespace SpaceMaker.Models
{
    internal class ReservationItem
    {
        public Reservation Reservation { get; set; } = null!;
        public string DisplayLine1 { get; set; } = "";
        public string DisplayLine2 { get; set; } = "";
    }
}
