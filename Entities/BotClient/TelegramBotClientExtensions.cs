using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace HouseFinderWebBot.BotClient.Extensions
{
    public static class TelegramBotClientExtensions
    {
        public static Task<Message> SendApartmentMessages(this ITelegramBotClient botClient, string chatId, ApartmentInfo apartment)
        {
            return botClient.SendPhotoAsync(
                       chatId: new ChatId(chatId),
                       photo: apartment.ImageRef,
                       caption: $"<b>Rua</b>: {apartment.Rua},\n" +
                                $"<b>Bairro</b>: {apartment.Bairro}, {apartment.Cidade},\n" +
                                $"<b>Área</b>: {apartment.Area}m²,\n" +
                                $"<b>Aluguel</b>: R$ {apartment.Aluguel.ToString("F", new System.Globalization.CultureInfo("pt-BR"))},\n" +
                                $"<b>Valor Total</b>: R$ {apartment.Total.ToString("F", new System.Globalization.CultureInfo("pt-BR"))},\n" +
                                $"<b>Link</b>: <a>{apartment.Href}</a>",
                       parseMode: ParseMode.Html
                     );
        }

        public static Task SendInitialMessage(this ITelegramBotClient botClient, string chatId, List<ApartmentInfo> apartments)
        {
            if (apartments.Count == 1)
            {
                return botClient.SendTextMessageAsync(
                      chatId: new ChatId(chatId),
                      text: $"*Hello! I have a new apartment for you!*",
                      parseMode: ParseMode.Markdown
                    );
            }
            else if (apartments.Any())
            {
                return botClient.SendTextMessageAsync(
                      chatId: new ChatId(chatId),
                      text: $"*Hello! I have new apartments for you!*",
                      parseMode: ParseMode.Markdown
                    );
            }

            Console.WriteLine("No new apartment!");
            return Task.FromResult<object>(null);
        }
    }
}
