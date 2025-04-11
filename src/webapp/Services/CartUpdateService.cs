using System;

namespace dotnetfashionassistant.Services
{
    public class CartUpdateService
    {
        // Event to notify subscribers when the cart has been updated
        public event Action? CartUpdated;
        
        // Method to trigger the update notification
        public void NotifyCartUpdated()
        {
            CartUpdated?.Invoke();
        }
    }
}
