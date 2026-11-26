<!-- source: /files/documentos/propuesta/postulacion-proyecto-caudales.pdf -->
# Postulación del proyecto de investigación PISF-25-03

A. I. PROPUESTA DEL PROYECTO
## 1. Información básica del proyecto
## 1.1 Tipo de proyecto
SEMILLA
## 1.2 Tipo de investigación
Aplicada
## 1.3 Unidad Asociada
DEPARTAMENTO DE MATEMATICA

## 2. Lineas de investigación
DEPARTAMENTO DE
MATEMATICA

LPI-DM-2023-04

MODELIZACIóN ESTOCáSTICA TEóRICA Y APLICADA

DEPARTAMENTO DE
INGENIERIA CIVIL Y
AMBIENTAL

LI-DICA-2023-01

GEOGRAFíA Y PAISAJE

DEPARTAMENTO DE
INGENIERIA CIVIL Y
AMBIENTAL

LPI-DICA-2023-01

HIDROLOGíA, HIDROGEOLOGíA Y RECURSOS HíDRICOS

DEPARTAMENTO DE
INGENIERIA CIVIL Y
AMBIENTAL

LI-DICA-2023-06

METEOROLOGíA Y CLIMATOLOGíA APLICADA

DESCRIPCIÓN DEL PROYECTO
## 3. Presentación del Proyecto
## 3.1. Título del proyecto
Metodología para la regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la
homogeneización de variables hidrometeorológicas.

## 3.2. Resumen del proyecto
La gestión integral de los recursos hídricos en Ecuador enfrenta limitaciones críticas debido a la baja calidad de
las series hidro-meteorológicas, las cuales presentan vacíos de información e inhomogeneidades que dificultan
la planificación hídrica y la resiliencia climática. El presente proyecto propone un marco metodológico
estructurado en tres fases para la regionalización de caudales. En la primera fase, se aplica el Análisis de Datos
Funcionales (FDA) para el control de calidad, suavizamiento mediante bases de Fourier e imputación de
variables meteorológicas, tratando las series como trayectorias continuas. Posteriormente, se realiza un
clustering funcional para identificar regiones meteorológicas homogéneas basadas en su comportamiento
dinámico.
La segunda fase implementa una homogeneización robusta utilizando el paquete Climatol, aplicada tanto a
variables meteorológicas como a caudales. Este proceso se fortalece mediante técnicas de remuestreo
Bootstrap (Moving Block y Stationary Bootstrap) para establecer umbrales de detección de quiebres (SNHT)
objetivos con un 95% de confianza, eliminando la subjetividad visual tradicional. Finalmente, la tercera fase
consiste en la regionalización hidrológica directa, donde los caudales homogeneizados se reconstruyen en su

forma funcional (hidrogramas) mediante bases B-splines o Fourier. Se emplea clustering funcional para agrupar
cuencas según la similitud de su respuesta y dinámica temporal (tiempos de crecida y estiaje), superando las
limitaciones de la proximidad geográfica.
Esta investigación se alinea con el Plan Estratégico de Desarrollo Institucional (PEDI) de la Escuela Politécnica
Nacional(EPN) 2024-2029 (OEI 2) y los ODS 6, 9, 13 y 17, mediante la vinculación con el Instituto Nacional de
Meteorología e Hidrología(INAMHI) para fortalecer modelos operativos y Sistemas de Alerta Temprana. El
proyecto culminará con la publicación de dos artículos científicos indexados enfocados en la homogeneización
meteorológica, homogeneización hidrológica y la regionalización funcional de caudales, proporcionando una
base de datos de alta fidelidad para la seguridad hídrica, fortaleciendo la calidad de los datos y su
interoperabilidad para la planificación por cuencas y la adaptación al cambio climático en Ecuador.
3.3. ¿Este proyecto está relacionado con saberes ancestrales?

NO

3.4. ¿La propuesta tiene potencial de transferencia tecnológica e innovación?

SI

El proyecto presenta un alto potencial de transferencia tecnológica al desarrollar un marco metodológico avanzado
que integra el Análisis de Datos Funcionales (FDA) y la homogeneización robusta con técnicas Bootstrap para depurar
series hidro-meteorológicas nacionales. Este enfoque innovador, que utiliza una clasificación basada en la dinámica
continua de los hidrogramas y algoritmos de clustering funcional, permite superar las limitaciones de la proximidad
geográfica tradicional al agrupar estaciones por similitud en su respuesta temporal (tiempos de crecida y estiaje).
Además, la incorporación de métodos de remuestreo (Moving Block y Stationary Bootstrap) para la detección objetiva
de quiebres en las series constituye un salto tecnológico sobre los métodos visuales convencionales, estableciendo un
precedente que puede ser transferido directamente a organismos rectores como el INAMHI para fortalecer la
soberanía y fidelidad de los datos hídricos nacionales.
La capacidad de este modelo para transformar registros discretos en curvas continuas mediante bases de Fourier o Bsplines permite realizar reconstrucciones precisas de caudales y facilita su adaptación a diversos escenarios
geográficos y climáticos del Ecuador. Al generar bases de datos de alta fidelidad con incertidumbre cuantificada, la
propuesta permite a los tomadores de decisiones diseñar estrategias de planificación hídrica y resiliencia climática
basadas en evidencia científica sólida. Esta versatilidad metodológica asegura que el conocimiento generado sea
aplicable tanto en el sector público como en el privado para el diseño de infraestructura hidráulica resiliente y la
gestión de Sistemas de Alerta Temprana.
Finalmente, la implementación de esta innovación tecnológica impulsa el cumplimiento de los Objetivos de Desarrollo
Sostenible (ODS 6, 9, 13 y 17) al proveer herramientas avanzadas para la gestión integrada de los recursos hídricos y
la adaptación al cambio climático. La difusión de los resultados mediante protocolos técnicos y la vinculación
institucional garantiza que la metodología no solo sea un avance académico, sino una solución práctica transferible
que mejore la seguridad hídrica a nivel nacional.

## 4. Palabras clave
Caudales, Análisis de Datos Funcionales, Clustering Funcional, Técnicas Bootstrap, Homogenización,
## 5. Objetivos del Proyecto
## 5.1. Objetivo General

Desarrollar un modelo matemático de regionalización, homogenización e imputación funcional de caudales en
Ecuador, basado en el análisis de datos funcionales (FDA) y técnicas de remuestreo Bootstrap, para cumplir con el
OEI2 del PEDI y aportar estratégicamente a la Agenda 2030: al ODS 6 (Agua Limpia y Saneamiento) mediante la
provisión de series históricas completas que aseguren una gestión integrada de recursos hídricos; al ODS 9
(Industria, Innovación e Infraestructura) al generar datos precisos para el diseño de infraestructura hidráulica
resiliente; al ODS 13 (Acción por el Clima) mediante el monitoreo de indicadores de alteración hidrológica (IHA)
para la adaptación al cambio climático; y al ODS 17 (Alianzas para lograr los Objetivos) a través de la transferencia
tecnológica y la creación de bases de datos de alta calidad para el fortalecimiento institucional de los organismos
rectores del agua.
## 5.2. Objetivos Específicos
a. Ejecutar el proceso de suavizamiento, imputación y agrupamiento funcional de variables meteorológicas
mediante el Análisis de Datos Funcionales (FDA), utilizando bases de Fourier para capturar la estacionalidad de
las series y clustering, definiendo grupos de estaciones con comportamiento hidrológico similar para la
transferencia de datos.
b. Desarrollar un protocolo de homogeneización hidrometeorológica robusta mediante el uso del paquete
Climatol, fortalecido con técnicas de remuestreo Moving Block Bootstrap (MBB) y Stationary Bootstrap (SB) para
la definición de umbrales de detección de quiebres con un 95% de confianza.
c. Diseñar una metodología de regionalización de los caudales mediante clustering funcional, para transformar
registros discretos en curvas continuas y ejecutar el llenado de vacíos en las series de caudales.
d. Difundir los resultados mediante protocolos técnicos para organismos rectores y la publicación de dos artículos
científicos en revistas indexadas (Scopus/WoS), cumpliendo con las metas de producción científica y vinculación
institucional.
## 6. Hipótesis (opcional)

## 7. Resultados Esperados
OBJETIVO

RESULTADO

Ejecutar el proceso de
Base de datos meteorológica (precipitación) depurada y regionalización meteorológica
suavizamiento,
inicial basada en la similitud de comportamientos dinámicos continuos, contribuyendo a
imputación y
la gestión sostenible del agua (ODS 6).
agrupamiento funcional
de variables
meteorológicas mediante
el Análisis de Datos
Funcionales (FDA),
utilizando bases de
Fourier para capturar la
estacionalidad de las
series y clustering,
definiendo grupos de
estaciones con
comportamiento
hidrológico similar para la
transferencia de datos.

Desarrollar un protocolo Series de precipitación y caudal homogeneizadas estadísticamente, con quiebres
de homogeneización
corregidos y niveles de incertidumbre cuantificados objetivamente,fomentando la
hidrometeorológica
innovación en infraestructura de datos (ODS 9).
robusta mediante el uso
del paquete Climatol,
fortalecido con técnicas
de remuestreo Moving
Block Bootstrap (MBB) y
Stationary Bootstrap (SB)
para la definición de
umbrales de detección de
quiebres con un 95% de
confianza.
Diseñar una metodología Mapa de regiones hidrológicamente homogéneas del área de estudio. Base de datos de
de regionalización de los caudal suavizada y corregida, fortaleciendo la resiliencia y la capacidad de adaptación
caudales mediante
mediante el monitoreo lo que puede considerarse fundamental para los sistemas de
clustering funcional, para alerta temprana de inundaciones y sequías (ODS 13).
transformar registros
discretos en curvas
continuas y ejecutar el
llenado de vacíos en las
series de caudales.
Difundir los resultados
Dos artículos científicos enviados y/o publicados en revistas de alto impacto que
mediante protocolos
posicionan la investigación técnica de la EPN a nivel internacional. (OEI 2 - PEDI) y
técnicos para organismos contribuyendo con la ODS 17 al fomentar la transferencia tecnológica entre la academia
rectores y la publicación EPN y el INAMHI para mejorar la soberanía de los datos nacionales.
de dos artículos
científicos en revistas
indexadas (Scopus/WoS),
cumpliendo con las metas
de producción científica y
vinculación institucional.

## 8. Impacto de la investigación
Objetivos de desarrollo sostenible
Objetivo 9: Industria, innovación e infraestructura
Objetivo 13: Acción por el clima
Objetivo 17: Alianzas para lograr objetivos
Objetivo 6: Agua limpia y saneamiento
Justificación

Justificación de la contribución a los Objetivos de Desarrollo Sostenible (ODS)
El proyecto se alinea estratégicamente con la Agenda 2030 al tecnificar la gestión de la información hidro-meteorológica nacional a través de
modelos matemáticos de vanguardia. Su impacto se desglosa en los siguientes objetivos:
ODS 6 (Agua Limpia y Saneamiento): Contribuye mediante la implementación de un modelo de regionalización e imputación funcional (FDA) que
trata los hidrogramas como objetos continuos. Estas bases de datos son el insumo crítico para el cálculo de caudales ecológicos y la planificación
hidrológica por cuencas, asegurando una gestión sostenible y equitativa de los recursos hídricos, incluso en regiones con escasa instrumentación.
ODS 9 (Industria, Innovación e Infraestructura): Introduce innovación tecnológica al sustituir los métodos tradicionales de proximidad geográfica por
un clustering funcional basado en la dinámica temporal de las series (tiempos de crecida y estiaje). Esta sofisticación estadística, que utiliza bases
de Fourier y B-splines, moderniza la infraestructura de datos del país, permitiendo un diseño de obras hidráulicas basado en información precisa y
resiliente.
ODS 13 (Acción por el Clima): El proyecto fortalece la acción climática al homogenizar series hidrometeorológicas mediante el paquete Climatol y
aplicar técnicas de remuestreo Bootstrap (Moving Block y Stationary Bootstrap) para cuantificar la incertidumbre de forma objetiva. Esto proporciona
datos robustos sobre eventos extremos, fundamentales para el diseño de políticas de adaptación climática y la optimización de Sistemas de Alerta
Temprana.
ODS 17 (Alianzas para lograr los objetivos): Impacta al establecer un puente de cooperación técnica entre la academia (EPN) y los organismos
rectores (INAMHI). La transferencia de algoritmos funcionales y protocolos de homogeneización robusta fortalece la capacidad técnica institucional y
promueve la soberanía de los datos nacionales, facilitando el intercambio de conocimiento científico de alto nivel.
El proyecto provee datos validados para priorizar medidas de adaptación hídrica y para el seguimiento de pérdidas y daños. La entrega de series
robustas con incertidumbre cuantificada mediante Bootstrap, mejora los insumos de vulnerabilidad y los criterios de diseño climático-resiliente en
agua potable, riego y energía.

Describir la importancia del proyecto para el país desde el punto de vista científico, social, económico u otro impacto (Obligatorio

Importancia del proyecto para el país
La trascendencia de este proyecto radica en su capacidad para transformar registros históricos crudos del INAMHI en información
estratégica de alta fidelidad para el país. Su importancia se manifiesta en tres dimensiones fundamentales:
Impacto Científico y Tecnológico: El proyecto posiciona a la EPN a la vanguardia de la hidrología regional mediante el uso de
Análisis de Datos Funcionales (FDA) y técnicas de remuestreo Bootstrap (MBB y SB). Al tratar los hidrogramas como objetos
continuos y regionalizar las cuencas mediante clustering funcional basado en su dinámica temporal, en lugar de la simple

Su propuesta de proyecto se enmarca en la siguiente categoría de interés público para la institución

Gestión Integral de la Construcción, Medio Ambiente y Recursos Naturales

## 9. Estado del arte

A. II. DETALLES DEL PROYECTO
## 10. Descripción detallada de proyecto incluida su metodología

## 11. Descripción de equipos disponibles
EQUIPO

CÓDIGO

DEPARTAMENTO

UBICACIÓN

COMPUTADORA
PORTATIL

PF5YMWRB

DEPARTAMENTO DE MATEMATICA

Departamento de Matemática
Edificio No. 3

S21CI70325005 DEPARTAMENTO DE MATEMATICA

Departamento de Matemática
Edificio No. 3

COMPUTADOR DE
ESCRITORIO

## 12. Vinculación con Posgrados
No aplica.

## 13. Justificación del presupuesto para la ejecución del proyecto
Item presupuesto
Contratación de servicios
profesionales

Contribución

Justificación

Asistente de Investigación:
El proyecto requiere un equipo
Apoyo en el Pre-procesamiento y
técnico operativo capaz de gestionar
Suavizamiento (OE1):
décadas de registros hidroEjecutar la limpieza, depuración y
meteorológicos nacionales y ejecutar
organización de las series históricas de modelos computacionales de alta
variables meteorológicas.
complejidad. La participación de un
Colaborar en la aplicación de técnicas de Asistente de Investigación es
suavizamiento mediante bases de Fourier fundamental para las fases críticas
para transformar registros discretos en de curación, depuración y
trayectorias continuas, bajo la supervisión organización de las bases de datos
del investigador principal.
masivas del INAMHI, asegurando
Soporte en la Homogeneización Robusta que el insumo para los modelos
(OE2):
matemáticos de Análisis de Datos
Apoyar en la ejecución del paquete
Funcionales (FDA) sea de alta
Climatol para la detección de quiebres
calidad.
estructurales en las series.
Este perfil brindará el soporte
Asistir en la implementación de
necesario para el procesamiento de
algoritmos de Moving Block y Stationary series temporales continuas y la
Bootstrap para el cálculo de umbrales de ejecución de protocolos de
detección y la cuantificación inicial de la homogeneización robusta. Su rol es
incertidumbre en las bases de datos.
estratégico para garantizar la
Asistencia en la Regionalización de
operatividad de los algoritmos de
Caudales (OE3):
remuestreo Bootstrap y la correcta
Organizar las matrices de datos de
estructuración de los hidrogramas
caudal para la reconstrucción funcional funcionales, permitiendo que el
mediante bases B-splines.
equipo investigador principal cumpla
Colaborar en la ejecución de algoritmos con los estándares de rigor
de clustering funcional para agrupar
estadístico necesarios para la
cuencas según su dinámica temporal
posterior publicación de los
(crecidas y estiajes), asegurando la
resultados en revistas de alto impacto
consistencia de los resultados para la
(Scopus/WoS).
fase de transferencia técnica.

Item presupuesto
Pago de publicaciones

Contribución

Justificación

El pago de las tarifas de publicación es el La asignación presupuestaria para el
mecanismo que materializa el Objetivo pago de publicaciones científicas es
Específico 4 (Difusión), permitiendo que fundamental para garantizar que los
la producción científica del equipo se
resultados de esta investigación
posicione en revistas del primer cuartil de alcancen el máximo impacto global.
Scopus/WoS. Esta acción representa la Esta inversión se justifica por el
validación externa por pares de la
compromiso de publicar dos artículos
rigurosidad con la que se ejecutó el
científicos de alto nivel: el primero
suavizamiento e imputación funcional
enfocado en la homogeneización
(OE1), la homogeneización estadística robusta de variables meteorológicas
con remuestreo (OE2) y la
mediante técnicas Bootstrap y el
regionalización hidrológica mediante
segundo en la regionalización
dinámicas temporales (OE3). Al asegurar funcional de caudales. El pago de las
la publicación de los artículos previstos, tarifas de procesamiento de artículos
se cumple con los indicadores de
(APC) permite que el conocimiento
producción científica del PEDI EPN (OEI generado en la EPN sea de Acceso
2), transformando los datos técnicos en Abierto (Open Access), facilitando
un activo académico internacional.
que investigadores, tomadores de
Además, la difusión en revistas de alto decisiones y organismos técnicos
impacto garantiza que los protocolos
como el INAMHI accedan a
desarrollados bajo FDA y Bootstrap sean metodologías de vanguardia en
reconocidos como estándares científicos hidrología funcional. Esto asegura
válidos para su implementación en la
que la ciencia producida sea
planificación hídrica nacional.
auditable, reproducible y útil para la
modelización de hidrogramas
continuos y la resiliencia climática en
el Ecuador.

Item presupuesto

Contribución

Justificación

Atención a delegados /
La colaboración técnica internacional
Debido a la naturaleza altamente
Investigadores colaboradores mediante plataformas digitales es un
especializada del Análisis de Datos
externos
componente fundamental para la
Funcionales (FDA) y la
validación externa de la metodología
Regionalización Hidrológica mediante
propuesta. Al interactuar con
dinámica temporal, el proyecto
investigadores líderes en estadística de contempla un esquema de
forma remota, se propicia un intercambio colaboración internacional telemática,
dinámico de criterios metodológicos que sin incurrir en gastos al proyecto..
enriquecerá la regionalización
Esta asignación se justifica por la
meteorológica (OE1) y la regionalización necesidad de recibir asesoría técnica
funcional de caudales mediante B-splines y retroalimentación de expertos
(OE3). Este apoyo telemático asegura
globales en estadística y modelado
una revisión experta de los procesos de de trayectorias continuas. Al optar
homogeneización robusta (OE2) y la
por un formato telemático, se
cuantificación de la incertidumbre
optimizan los recursos para
mediante técnicas de remuestreo
garantizar un acompañamiento
Bootstrap. Finalmente, la exposición de continuo en el desarrollo del
los resultados en foros científicos
clustering funcional y el
globales fortalece la calidad de los
suavizamiento mediante bases de
manuscritos, facilitando su aceptación en Fourier, asegurando que la
revistas indexadas (Scopus/WoS) y
metodología aplicada en las cuencas
potenciando el prestigio académico de la ecuatorianas cumpla con los
EPN en la modelización avanzada de
estándares internacionales de
series hidro-meteorológicas (OE4).
precisión. Asimismo, este rubro
financiará la participación del equipo
en congresos internacionales de
prestigio en modalidad virtual,
permitiendo la difusión de los
hallazgos técnicos sobre la
reconstrucción de hidrogramas sin
incurrir en costos logísticos de
desplazamiento.

B. DATOS INFORMATIVOS DEL EQUIPO
## 1. Infomación del Director, Codirector, Colaboradores y Colaboradores Técnicos
Nombres y
Apellidos

Cédula

HSS*

Unidad
Académica

ADRIANA
UQUILLAS
ANDRADE

DEPARTAMENTO
DE MATEMATICA

MIGUEL
ALFONSO
FLORES
SANCHEZ
XAVIER
EDUARDO
ZAPATA RIOS

Rol

Cargo

PROFESOR
AGREGADO A
TIEMPO
COMPLETO (NIVEL
3, GRADO 5)
PROFESOR
DEPARTAMENTO
AGREGADO A
Codirector
DE MATEMATICA
TIEMPO
COMPLETO (NIVEL
3, GRADO 5)
PROFESOR
DEPARTAMENTO
AGREGADO A
DE INGENIERIA Colaborador
TIEMPO
CIVIL Y
COMPLETO (NIVEL
AMBIENTAL
1, GRADO 3)

Director

Título de Mayor
Nivel
DOCTOR EN
CIENCIAS
Doctor en
Estadística e
Investigación
Operativa
PhD EN
HIDROLOGÍA

Nombres y
Apellidos

Cédula

HSS*

Ramón A
Mollineda
Cárdenas

null

Sandra Janeth
Torres Paguay

Unidad
Académica

Rol

Cargo

Institute of New
Imaging
Technologies
(INIT) Universitat
Jaume I, Castellón
de la Plana,

Colaborador
Externo

Profesor Titular e
Investigador

Instituto Nacional
de Meteorología e
Hidrología

Colaborador
Externo

Especialista en
Metereología

Título de Mayor
Nivel
Doctor en
informática por
la Universidad
Politécnica de
Valencia
(España), PhD
INGENIERA
AMBIENTAL

*HSS =Horas Semana Semestre: Es el número de horas que se dedica por semana a la investigación. Este
número de horas se mantiene para todo el semestre

## 2. Investigaciones Previas del equipo

C. INFORMACIÓN ADICIONAL
## 1. Información de presentación previa de la propuesta
Tipo de proyecto

Año de postulación

No aplica

No aplica.

¿Este proyecto proviene de algún proyecto de vinculación existente?

NO

## 2. Sugerencia de potenciales revisores

NOMBRES

APELLIDOS

INSTITUCION

EMAIL

David Fernando

Galarza Muñoz

[correo omitido]

Boris

Ochoa Tocachi

Iván

Ríos

Instituto Nacional de
Meteorología e
Hidrología (INAMHI)
ATUK Consultoría
Estratégica
Universidad Nacional
de Chimborazo

[correo omitido]
[correo omitido]

## 3. Exclusión de potenciales revisores
No aplica.

CRONOGRAMA DE ACTIVIDADES
NOMBRE OBJETIVO

Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes
10 11 12

AÑO 1
Objetivo Específico: Ejecutar el proceso de
suavizamiento, imputación y agrupamiento funcional de
variables meteorológicas mediante el Análisis de Datos
Funcionales (FDA), utilizando bases de Fourier para
capturar la estacionalidad de las series y clustering,
definiendo grupos de estaciones con comportamiento
hidrológico similar para la transferencia de datos.
Actividad: Levantamiento de datos series de
Actividad: Suavizamiento e imputación de datos
Actividad: Regionalización de precipitación mediante
Objetivo Específico: Desarrollar un protocolo de
homogeneización hidrometeorológica robusta mediante el
uso del paquete Climatol, fortalecido con técnicas de
remuestreo Moving Block Bootstrap (MBB) y Stationary
Bootstrap (SB) para la definición de umbrales de
detección de quiebres con un 95% de confianza.
Actividad: Levantamiento de datos de caudal y detección
Actividad: Estimación de umbrales mediante técnicas
Actividad: Generación de series homogeneizadas con
Objetivo Específico: Diseñar una metodología de
regionalización de los caudales mediante clustering
funcional, para transformar registros discretos en curvas
continuas y ejecutar el llenado de vacíos en las series de
caudales.
Actividad: Reconstrucción de hidrogramas mediante
Actividad: Ejecución de algoritmos de clustering
Actividad: Mapa de regiones hidrológicamente
Objetivo Específico: Difundir los resultados mediante
protocolos técnicos para organismos rectores y la
publicación de dos artículos científicos en revistas
indexadas (Scopus/WoS), cumpliendo con las metas de
producción científica y vinculación institucional.
Actividad: Redacción del artículo científico 1
Actividad: Redacción del artículo científico 2 (Caudales)
Actividad: Resultado esperado: Dos artículos científicos

X

X

X
X

X
X

X

X

X
X

X
X

X

X

AÑO 2
Objetivo Específico: Ejecutar el proceso de
suavizamiento, imputación y agrupamiento funcional de
variables meteorológicas mediante el Análisis de Datos
Funcionales (FDA), utilizando bases de Fourier para
capturar la estacionalidad de las series y clustering,
definiendo grupos de estaciones con comportamiento
hidrológico similar para la transferencia de datos.
Actividad: Levantamiento de datos series de
Actividad: Suavizamiento e imputación de datos
Actividad: Regionalización de precipitación mediante

X

X
X

NOMBRE OBJETIVO
Objetivo Específico: Desarrollar un protocolo de
homogeneización hidrometeorológica robusta mediante el
uso del paquete Climatol, fortalecido con técnicas de
remuestreo Moving Block Bootstrap (MBB) y Stationary
Bootstrap (SB) para la definición de umbrales de
detección de quiebres con un 95% de confianza.
Actividad: Levantamiento de datos de caudal y detección
Actividad: Estimación de umbrales mediante técnicas
Actividad: Generación de series homogeneizadas con
Objetivo Específico: Diseñar una metodología de
regionalización de los caudales mediante clustering
funcional, para transformar registros discretos en curvas
continuas y ejecutar el llenado de vacíos en las series de
caudales.
Actividad: Reconstrucción de hidrogramas mediante
Actividad: Ejecución de algoritmos de clustering
Actividad: Mapa de regiones hidrológicamente
Objetivo Específico: Difundir los resultados mediante
protocolos técnicos para organismos rectores y la
publicación de dos artículos científicos en revistas
indexadas (Scopus/WoS), cumpliendo con las metas de
producción científica y vinculación institucional.
Actividad: Redacción del artículo científico 1
Actividad: Redacción del artículo científico 2 (Caudales)
Actividad: Resultado esperado: Dos artículos científicos

Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes Mes
10 11 12

X
X

X

X
X

X
X

X
X
X

X
X
X

PRESUPUESTO
Lista de items

Cantidad

Unidad

mes

Precio
Unitario

Precio
Total

SEMESTRE 1
## 1. Contratación de servicios profesionales
Asistente de investigación
Subtotal 1
## 2. Contratación de ayudantes de investigación

$

986.0 $
$

5916.0
5916.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

Subtotal 2
## 3. Maquinaria y equipo especializado
Subtotal 3
## 4. Equipo informático
Subtotal 4
## 5. Insumos y Reactivos
Subtotal 5
## 6. Análisis de laboratorio
Subtotal 6
## 7. Literatura especializada
Subtotal 7
## 8. Salidas de campo y de muestreo
Subtotal 8
## 9. Ponencias nacionales, capacitaciones y/o visitas
Subtotal 9
## 10. Ponencias en el exterior, capacitaciones y/o
Subtotal 10
## 11. Pago de inscripciones
Subtotal 11
## 12. Atención a delegados / Investigadores
Subtotal 12
## 13. Pago de publicaciones
Subtotal 13

Lista de items

Cantidad

Precio
Unitario

Unidad

TOTAL SEMESTRE 1

Precio
Total
$

5916.0

$

986.0 $
$

5916.0
5916.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

$

0.0 $
$

0.0
0.0

Publicación $

1584.0 $
$

3168.0
3168.0

SEMESTRE 2
## 1. Contratación de servicios profesionales
Asistente de investigación
Subtotal 1
## 2. Contratación de ayudantes de investigación

mes

Subtotal 2
## 3. Maquinaria y equipo especializado
Subtotal 3
## 4. Equipo informático
Subtotal 4
## 5. Insumos y Reactivos
Subtotal 5
## 6. Análisis de laboratorio
Subtotal 6
## 7. Literatura especializada
Subtotal 7
## 8. Salidas de campo y de muestreo
Subtotal 8
## 9. Ponencias nacionales, capacitaciones y/o visitas
Subtotal 9
## 10. Ponencias en el exterior, capacitaciones y/o
Subtotal 10
## 11. Pago de inscripciones
Subtotal 11
## 12. Atención a delegados / Investigadores
Subtotal 12
## 13. Pago de publicaciones
Pago de publicaciones
Subtotal 13

Lista de items

Cantidad

Unidad

Precio
Unitario

Precio
Total

TOTAL SEMESTRE 2

$

9084.0

TOTAL PROYECTO

$

15000.0

D. DECLARACIÓN FINAL
Declaración del codirector de proyecto
Declaro que conozco que la normativa de proyectos establece que, en caso de ausencia del Director del
proyecto en ejecución, la Dirección de Investigación notificará el cambio de Director a favor del Codirector.

Yo, MIGUEL ALFONSO FLORES SANCHEZ
Declaro que acepto los puntos descritos en esta sección.

Declaración del director de proyecto
El equipo de investigadores, representado por el Director del Proyecto declara lo siguiente:
Que el presente proyecto es una creación original de mi autoría y del equipo de investigadores, y por tanto
asumimos la completa responsabilidad legal en caso de que un tercero alegue la titularidad de los
derechos intelectuales del proyecto, exonerando a la EPN de cualquier acción legal que se derive por esta
causa.
Que aceptamos conocer y cumplir con la normativa vigente para la gestión de proyectos y las Bases para
la Convocatoria para la presentación de Proyectos de Investigación, Vinculación y Transferencia
Tecnológica con financiamiento 2025.
Que el presente proyecto no ha sido ejecutado, ni está actualmente siendo evaluado en ninguna
convocatoria de otra institución pública o privada. El incumplimiento será causal para que el proyecto no
sea tomado en consideración.
Que si el proyecto genera algún producto o procedimiento susceptible de obtener derechos de propiedad
intelectual, de los cuales se deriven beneficios, aceptamos que éstos serán compartidos entre los
investigadores y la institución o las instituciones participantes en el proyecto, conforme a lo establecido en
el COESC.
Que el equipo de investigadores y/o instituciones participantes se comprometen a mantener la
confidencialidad de la información si ésta podría ser susceptible de protección por patentes, y solicitar la
valoración de propiedad intelectual respectiva previa a cualquier publicación o difusión.
Que para el caso de derechos de autor otorgamos una licencia de uso exclusivo con fines académicos
para la o las instituciones participantes en el proyecto.

Yo, ADRIANA UQUILLAS ANDRADE
Declaro que acepto los puntos descritos en esta sección.

Firmado electrónicamente por:

ADRIANA UQUILLAS
ANDRADE
Validar únicamente con FirmaEC
