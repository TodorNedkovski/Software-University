﻿namespace Cinema.DataProcessor
{
    using AutoMapper;
    using Cinema.Data.Models;
    using Cinema.Data.Models.Enums;
    using Cinema.DataProcessor.ImportDto;
    using Data;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Serialization;
    using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

    public class Deserializer
    {
        private const string ErrorMessage = "Invalid data!";
        private const string SuccessfulImportMovie
            = "Successfully imported {0} with genre {1} and rating {2}!";
        private const string SuccessfulImportHallSeat
            = "Successfully imported {0}({1}) with {2} seats!";
        private const string SuccessfulImportProjection
            = "Successfully imported projection {0} on {1}!";
        private const string SuccessfulImportCustomerTicket
            = "Successfully imported customer {0} {1} with bought tickets: {2}!";

        public static string ImportMovies(CinemaContext context, string jsonString)
        {
            var moviesDto = JsonConvert.DeserializeObject<ImportMovieDto[]>(jsonString);

            List<Movie> movies = new List<Movie>();

            StringBuilder sb = new StringBuilder();

            foreach (var movieDto in moviesDto)
            {
                bool isTitleExist = movies.Any(m => m.Title == movieDto.Title);
                bool isValidGenre = Enum.IsDefined(typeof(Genre), movieDto.Genre);

                if (isTitleExist == true || isValidGenre == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                var movie = Mapper.Map<Movie>(movieDto);

                bool isValidMovie = IsValid(movie);

                if (isValidMovie == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                movies.Add(movie);

                sb.AppendLine(string.Format(SuccessfulImportMovie,
                    movie.Title,
                    movie.Genre.ToString(),
                    movie.Rating.ToString("F2")));
            }

            context.Movies.AddRange(movies);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }

        public static string ImportHallSeats(CinemaContext context, string jsonString)
        {
            var hallsDto = JsonConvert.DeserializeObject<ImportHallWithSeatsDto[]>(jsonString);

            List<Hall> halls = new List<Hall>();

            StringBuilder sb = new StringBuilder();

            foreach (var hallDto in hallsDto)
            {
                var hall = Mapper.Map<Hall>(hallDto);

                bool isValidHall = IsValid(hall);
                bool isValidSeatsCount = IsValid(hallDto);

                if (isValidHall == false || isValidSeatsCount == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                for (int i = 0; i < hallDto.SeatsCount; i++)
                {
                    hall.Seats.Add(new Seat());
                }

                string projectionType = string.Empty;

                if (hall.Is3D == true && hall.Is4Dx == true)
                {
                    projectionType = "4Dx/3D";
                }
                else if (hall.Is3D == true)
                {
                    projectionType = "3D";
                }
                else if (hall.Is4Dx == true)
                {
                    projectionType = "4Dx";
                }
                else
                {
                    projectionType = "Normal";
                }

                halls.Add(hall);

                sb.AppendLine(string.Format(SuccessfulImportHallSeat,
                    hall.Name,
                    projectionType,
                    hall.Seats.Count()));
            }

            context.Halls.AddRange(halls);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }

        public static string ImportProjections(CinemaContext context, string xmlString)
        {
            var xmlSerializer = new XmlSerializer(
                typeof(ImportProjectionDto[]), new XmlRootAttribute("Projections"));
            var projectionsDto = (ImportProjectionDto[])xmlSerializer.Deserialize(new StringReader(xmlString));

            List<Projection> projections = new List<Projection>();

            StringBuilder sb = new StringBuilder();

            foreach (var projectionDto in projectionsDto)
            {
                var projection = Mapper.Map<Projection>(projectionDto);

                bool isValdiProjection = IsValid(projection);
                var targetMovie = context.Movies.Find(projectionDto.MovieId);
                var targetHall = context.Halls.Find(projectionDto.HallId);

                if (isValdiProjection == false || targetMovie == null || targetHall == null)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                projection.Movie = targetMovie;
                projection.Hall = targetHall;

                projections.Add(projection);

                sb.AppendLine(string.Format(SuccessfulImportProjection,
                    projection.Movie.Title,
                    projection.DateTime.ToString(@"MM/dd/yyyy")));
            }

            context.Projections.AddRange(projections);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }

        public static string ImportCustomerTickets(CinemaContext context, string xmlString)
        {
            var xmlSerializer = new XmlSerializer(typeof(ImportCustomerDto[]), new XmlRootAttribute("Customers"));
            var customersDto = (ImportCustomerDto[])xmlSerializer.Deserialize(new StringReader(xmlString));

            List<Customer> customers = new List<Customer>();

            StringBuilder sb = new StringBuilder();

            foreach (var customerDto in customersDto)
            {
                var customer = Mapper.Map<Customer>(customerDto);
                bool isValidCustomer = IsValid(customer);

                var projectionsIds = context.Projections.Select(p => p.Id).ToList();
                var isProjectionsExists = customerDto.CustomerTickets
                    .Select(t => t.ProjectionId)
                    .All(t => projectionsIds.Contains(t));

                if (isValidCustomer == false || isProjectionsExists == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                foreach (var ticketDto in customerDto.CustomerTickets)
                {
                    var ticket = Mapper.Map<Ticket>(ticketDto);

                    customer.Tickets.Add(ticket);
                }

                customers.Add(customer);

                sb.AppendLine(string.Format(SuccessfulImportCustomerTicket,
                    customer.FirstName,
                    customer.LastName,
                    customer.Tickets.Count()));
            }

            context.Customers.AddRange(customers);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }

        private static bool IsValid(object dto)
        {
            var validationContext = new ValidationContext(dto);
            var validationResult = new List<ValidationResult>();

            return Validator.TryValidateObject(dto, validationContext, validationResult, true);
        }
    }
}