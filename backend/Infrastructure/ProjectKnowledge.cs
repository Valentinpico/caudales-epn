namespace CaudalesPlatform.Api.Infrastructure;

/// <summary>
/// Static knowledge taken from the project proposal ("Postulación Proyecto Caudales"):
/// objectives, schedule activities, methods, team, institutions and SDGs.
/// Dynamic nodes (stations, datasets, documents) are added by KnowledgeGraphService.
/// </summary>
public static class ProjectKnowledge
{
    public const string ProjectId = "project:caudales";

    public record Node(string Id, string Kind, string Label, string? Description = null);
    public record Edge(string Source, string Target, string Relation);

    public static readonly Node[] Nodes =
    [
        new(ProjectId, "Project",
            "Regionalización de caudales en Ecuador (FDA + homogeneización)",
            "Metodología para la regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la homogeneización de variables hidrometeorológicas. Proyecto semilla, Departamento de Matemática, EPN."),

        new("objective:OE1", "Objective", "OE1 · Suavizamiento, imputación y agrupamiento funcional",
            "Ejecutar el proceso de suavizamiento, imputación y agrupamiento funcional de variables meteorológicas mediante FDA, utilizando bases de Fourier y clustering."),
        new("objective:OE2", "Objective", "OE2 · Homogeneización robusta",
            "Desarrollar un protocolo de homogeneización hidrometeorológica robusta mediante Climatol, con Moving Block Bootstrap y Stationary Bootstrap para umbrales de detección de quiebres al 95 % de confianza."),
        new("objective:OE3", "Objective", "OE3 · Regionalización de caudales",
            "Diseñar una metodología de regionalización de caudales mediante clustering funcional, transformando registros discretos en curvas continuas y llenando vacíos."),
        new("objective:OE4", "Objective", "OE4 · Difusión",
            "Difundir los resultados mediante protocolos técnicos para organismos rectores y dos artículos científicos indexados (Scopus/WoS)."),

        new("activity:A1.1", "Activity", "Levantamiento de datos de series de precipitación", "Año 1, meses 1–3."),
        new("activity:A1.2", "Activity", "Suavizamiento e imputación de datos", "Año 1, meses 4–5."),
        new("activity:A1.3", "Activity", "Regionalización de precipitación", "Año 1, meses 5–6."),
        new("activity:A2.1", "Activity", "Levantamiento de datos de caudal y detección de quiebres", "Año 1, meses 7–8."),
        new("activity:A2.2", "Activity", "Estimación de umbrales mediante técnicas Bootstrap", "Año 1, meses 8–9."),
        new("activity:A2.3", "Activity", "Generación de series homogeneizadas", "Año 1, meses 9–10."),
        new("activity:A3.1", "Activity", "Reconstrucción de hidrogramas", "Año 1, meses 10–12."),
        new("activity:A3.2", "Activity", "Ejecución de algoritmos de clustering funcional", "Año 1, mes 12; año 2, mes 1."),
        new("activity:A3.3", "Activity", "Mapa de regiones hidrológicamente homogéneas", "Año 2, meses 1–2."),
        new("activity:A4.1", "Activity", "Redacción del artículo científico 1", "Año 2, meses 3–6."),
        new("activity:A4.2", "Activity", "Redacción del artículo científico 2 (caudales)", "Año 2, meses 3–6."),

        new("method:fda", "Method", "Análisis de Datos Funcionales (FDA)", "Trata las series como trayectorias continuas."),
        new("method:fourier", "Method", "Bases de Fourier", "Suavizamiento que captura la estacionalidad de las series."),
        new("method:bsplines", "Method", "Bases B-splines", "Reconstrucción funcional de hidrogramas."),
        new("method:clustering", "Method", "Clustering funcional", "Agrupa estaciones o cuencas por similitud de su dinámica temporal."),
        new("method:climatol", "Method", "Climatol", "Paquete de R para homogeneización y relleno de series climáticas."),
        new("method:snht", "Method", "SNHT", "Standard Normal Homogeneity Test para detección de quiebres."),
        new("method:mbb", "Method", "Moving Block Bootstrap (MBB)", "Remuestreo por bloques para umbrales de detección."),
        new("method:sb", "Method", "Stationary Bootstrap (SB)", "Remuestreo con bloques de longitud aleatoria."),

        new("variable:precipitation", "Variable", "Precipitación", "Precipitación diaria (mm)."),
        new("variable:temperature", "Variable", "Temperatura", "Temperatura diaria (°C)."),
        new("variable:discharge", "Variable", "Caudal", "Caudal (m³/s). Datos por levantar (OE2)."),

        new("researcher:uquillas", "Researcher", "Adriana Uquillas Andrade", "Directora. Doctora en Ciencias."),
        new("researcher:flores", "Researcher", "Miguel Alfonso Flores Sánchez", "Codirector. Doctor en Estadística e Investigación Operativa."),
        new("researcher:zapata", "Researcher", "Xavier Eduardo Zapata Ríos", "Colaborador. PhD en Hidrología."),
        new("researcher:mollineda", "Researcher", "Ramón A. Mollineda Cárdenas", "Colaborador externo. Doctor en Informática."),
        new("researcher:torres", "Researcher", "Sandra Janeth Torres Paguay", "Colaboradora externa. Especialista en Meteorología."),

        new("institution:epn-dm", "Institution", "EPN · Departamento de Matemática"),
        new("institution:epn-dica", "Institution", "EPN · Departamento de Ingeniería Civil y Ambiental"),
        new("institution:inamhi", "Institution", "INAMHI", "Instituto Nacional de Meteorología e Hidrología."),
        new("institution:uji", "Institution", "Universitat Jaume I (INIT)", "Institute of New Imaging Technologies, Castellón."),

        new("sdg:6", "SDG", "ODS 6 · Agua limpia y saneamiento"),
        new("sdg:9", "SDG", "ODS 9 · Industria, innovación e infraestructura"),
        new("sdg:13", "SDG", "ODS 13 · Acción por el clima"),
        new("sdg:17", "SDG", "ODS 17 · Alianzas para lograr los objetivos"),
    ];

    public static readonly Edge[] Edges =
    [
        new(ProjectId, "objective:OE1", "TIENE_OBJETIVO"),
        new(ProjectId, "objective:OE2", "TIENE_OBJETIVO"),
        new(ProjectId, "objective:OE3", "TIENE_OBJETIVO"),
        new(ProjectId, "objective:OE4", "TIENE_OBJETIVO"),

        new("objective:OE1", "activity:A1.1", "TIENE_ACTIVIDAD"),
        new("objective:OE1", "activity:A1.2", "TIENE_ACTIVIDAD"),
        new("objective:OE1", "activity:A1.3", "TIENE_ACTIVIDAD"),
        new("objective:OE2", "activity:A2.1", "TIENE_ACTIVIDAD"),
        new("objective:OE2", "activity:A2.2", "TIENE_ACTIVIDAD"),
        new("objective:OE2", "activity:A2.3", "TIENE_ACTIVIDAD"),
        new("objective:OE3", "activity:A3.1", "TIENE_ACTIVIDAD"),
        new("objective:OE3", "activity:A3.2", "TIENE_ACTIVIDAD"),
        new("objective:OE3", "activity:A3.3", "TIENE_ACTIVIDAD"),
        new("objective:OE4", "activity:A4.1", "TIENE_ACTIVIDAD"),
        new("objective:OE4", "activity:A4.2", "TIENE_ACTIVIDAD"),

        new("activity:A1.2", "method:fda", "APLICA_METODO"),
        new("activity:A1.2", "method:fourier", "APLICA_METODO"),
        new("activity:A1.3", "method:clustering", "APLICA_METODO"),
        new("activity:A2.1", "method:snht", "APLICA_METODO"),
        new("activity:A2.2", "method:mbb", "APLICA_METODO"),
        new("activity:A2.2", "method:sb", "APLICA_METODO"),
        new("activity:A2.3", "method:climatol", "APLICA_METODO"),
        new("activity:A3.1", "method:bsplines", "APLICA_METODO"),
        new("activity:A3.1", "method:fda", "APLICA_METODO"),
        new("activity:A3.2", "method:clustering", "APLICA_METODO"),
        new("method:climatol", "method:snht", "USA"),
        new("activity:A2.1", "variable:discharge", "ANALIZA"),
        new("activity:A3.1", "variable:discharge", "ANALIZA"),

        new("researcher:uquillas", ProjectId, "DIRIGE"),
        new("researcher:flores", ProjectId, "CODIRIGE"),
        new("researcher:zapata", ProjectId, "COLABORA"),
        new("researcher:mollineda", ProjectId, "COLABORA"),
        new("researcher:torres", ProjectId, "COLABORA"),
        new("researcher:uquillas", "institution:epn-dm", "PERTENECE_A"),
        new("researcher:flores", "institution:epn-dm", "PERTENECE_A"),
        new("researcher:zapata", "institution:epn-dica", "PERTENECE_A"),
        new("researcher:mollineda", "institution:uji", "PERTENECE_A"),
        new("researcher:torres", "institution:inamhi", "PERTENECE_A"),

        new(ProjectId, "sdg:6", "CONTRIBUYE_A"),
        new(ProjectId, "sdg:9", "CONTRIBUYE_A"),
        new(ProjectId, "sdg:13", "CONTRIBUYE_A"),
        new(ProjectId, "sdg:17", "CONTRIBUYE_A"),
    ];
}
