# ESRI Mobile Geodatabase to GeoPackage Converter

## Overview

This project provides a suite of tools to facilitate the conversion of ESRI Mobile Geodatabases (`.geodatabase` SQLite files) to OGC GeoPackage (`.gpkg`) files. The conversion process is envisioned in two main stages:

1.  **Metadata Extraction:** The .NET components of this project thoroughly parse the Mobile Geodatabase to extract detailed schema, metadata, domains, subtypes, relationships, and other relevant information.
2.  **Data Conversion (via Manifold GIS):** The actual reading of feature data, geometry processing, and generation of the `.gpkg` file is intended to be performed by a **Manifold GIS Release 9 script**. This script would utilize the metadata extracted by the .NET components to accurately map and convert the data.

The project consists of three main components:

*   **`CoreConverter`**: A .NET Standard library responsible for connecting to the Mobile Geodatabase (which is an SQLite database), parsing its system tables (`GDB_Items`, `GDB_ItemRelationships`, `GDB_Domains`, etc.), and extracting a comprehensive metadata structure. It does not handle feature data or write GeoPackage files directly.
*   **`WebApi`**: An ASP.NET Core Web API that provides an HTTP endpoint to upload a Mobile Geodatabase file. It uses `CoreConverter` to process the uploaded file and currently returns the extracted metadata log.
*   **`WebApp`**: A Blazor Server application that provides a user interface for uploading the `.geodatabase` file, specifying conversion parameters, and viewing the extracted metadata from the `WebApi`.

**Crucial Dependency Note:** The `CoreConverter` library in its current state focuses on **metadata extraction only**. It does not perform the actual data conversion or GeoPackage file creation. This subsequent step is designed to be handled by a separate **Manifold GIS script** which would leverage the detailed metadata output from `CoreConverter`.

## Project Structure

The solution is organized into the following main projects:

*   `src/CoreConverter/`: The .NET Standard class library for geodatabase metadata parsing.
*   `src/WebApi/`: The ASP.NET Core Web API project.
*   `src/WebApp/`: The Blazor Server UI project.
*   `tests/WebApi.Tests/`: xUnit integration tests for the Web API.

## Prerequisites

*   **.NET 8 SDK** (or the version used during development).
*   **Manifold GIS Release 9:** Required for the full data conversion process (beyond the metadata extraction provided by this solution).

## Building the Solution

To build the entire solution, navigate to the root directory of the project in your terminal and run:

```bash
dotnet build EsriToGpkgConverter.sln
```

Alternatively, you can often just run `dotnet build` from the root.

## Running the Applications

### WebApi

1.  Navigate to the WebApi directory:
    ```bash
    cd src/WebApi
    ```
2.  Run the application:
    ```bash
    dotnet run
    ```
3.  The API will typically start on a local port, for example, `https://localhost:7123` or `http://localhost:5123`. Check the console output for the exact URL. (The test setup used `https://localhost:7123` as a placeholder example).

### WebApp

1.  Navigate to the WebApp directory:
    ```bash
    cd src/WebApp
    ```
2.  Run the application:
    ```bash
    dotnet run
    ```
3.  Access the WebApp in your browser using the URL provided in the console output (e.g., `https://localhost:7XYZ` or `http://localhost:5XYZ`).

**Configuration:** The `WebApp` needs to know the base URL of the running `WebApi`. This is configured in `src/WebApp/Program.cs`:

```csharp
// Example:
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7123") }); // Replace with your WebApi's actual URL
```

Ensure the `Uri` in this line matches the actual address where your `WebApi` is running.

## Using the Converter Tool

1.  Open the `WebApp` in your browser.
2.  Navigate to the "GDB Converter" page using the navigation menu.
3.  **Upload File:** Click the "Choose File" (or similar) button and select your ESRI Mobile Geodatabase file (typically a `.geodatabase` file, or a `.gdb` file that is a single SQLite database).
4.  **Target SRS (Optional):** Enter the desired target Spatial Reference System for the output. Defaults to "EPSG:4326".
5.  **Bounding Box (Optional):** If you want to filter by a bounding box, enter the Min X, Min Y, Max X, and Max Y coordinates.
6.  **Submit:** Click the "Upload and Process" button.
7.  The application will send the file to the `WebApi`. After processing by `CoreConverter`, the extracted metadata log will be displayed on the page.

## API Endpoint

The `WebApi` exposes the following endpoint for conversion:

*   **URL:** `POST /api/Convert/UploadAndProcess`
*   **Request Type:** `multipart/form-data`
    *   **`file`**: The `.geodatabase` file being uploaded.
    *   **`targetSrs`** (string, optional): The target SRS for the conversion (e.g., "EPSG:4326"). Defaults to "EPSG:4326" if not provided.
    *   **`bbox`** (string, optional): A comma-separated string representing the bounding box filter: "minX,minY,maxX,maxY" (e.g., "-100.0,40.0,-90.0,50.0").
*   **Success Response (200 OK):**
    JSON object containing the metadata log:
    ```json
    {
        "metadataLog": "Detailed metadata string from CoreConverter..."
    }
    ```
*   **Error Response:**
    Appropriate HTTP status code (e.g., 400 Bad Request, 500 Internal Server Error) with a JSON object containing error details:
    ```json
    {
        "message": "Error description...",
        "details": "Optional further details..."
    }
    ```

## Current State & Limitations

*   **Metadata Extraction Focus:** The `CoreConverter` library currently performs detailed schema and metadata extraction from the Mobile Geodatabase. This includes information about feature classes, tables, fields, domains, subtypes, relationships, spatial references, and basic topology/attribute rule indicators.
*   **No Data Conversion:** The system does **not** currently read actual feature data (geometries and attributes) or write them to GeoPackage files. This crucial step is intended to be implemented using a Manifold GIS script that would consume the metadata extracted by `CoreConverter`.
*   **ESRI-Specific Features:** Information about ESRI-specific features like detailed Topology rules and Attribute Rules is extracted for awareness. These features generally do not have direct equivalents in the GeoPackage standard and are not functionally translated.
*   **API Output:** The Web API currently returns the detailed metadata log generated by `CoreConverter`. A full implementation would involve the API orchestrating the call to the (future) Manifold GIS script and managing the resulting GeoPackage file (e.g., providing a download link).

## Future Development (Optional)

*   **Manifold GIS Script:** Develop the Manifold GIS Release 9 script that:
    *   Takes the path to a Mobile Geodatabase and the extracted metadata (potentially via `CoreConverter.dll` if it's made accessible as a COM object, or by parsing the log/structured output).
    *   Reads feature data from the source Mobile GDB.
    *   Performs geometry transformations (reprojection, clipping to bbox).
    *   Creates and populates a new GeoPackage (`.gpkg`) file.
*   **WebApi Enhancements:**
    *   Implement asynchronous job handling for long-running conversion processes.
    *   Integrate with the Manifold GIS script (e.g., via command-line execution or other interop methods).
    *   Manage temporary storage for uploaded and generated files.
    *   Provide an endpoint to download the generated GeoPackage file.
*   **CoreConverter Enhancements:**
    *   Potentially output metadata in a structured format (e.g., JSON) in addition to the human-readable log, to be more easily consumed by the Manifold script.
*   **WebApp Enhancements:**
    *   Display progress of the conversion.
    *   Provide a download link for the resulting GeoPackage file.
    *   More robust error display and user feedback.
