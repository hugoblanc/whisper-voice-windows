using WhisperVoice.Config;

namespace WhisperVoice.Processing;

/// <summary>
/// AI processing mode for post-transcription text enhancement.
/// Built-in modes are defined here; custom modes are loaded from AppConfig.
/// </summary>
public class AIMode
{
    public string Id { get; }
    public string Name { get; }
    public string? SystemPrompt { get; }
    public bool IsBuiltIn { get; }
    public bool IsCustom => !IsBuiltIn;
    public bool IsSuper => string.Equals(Id, "super", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this mode requires AI processing.
    /// </summary>
    public bool RequiresProcessing => !string.IsNullOrWhiteSpace(SystemPrompt);

    public AIMode(string id, string name, string? systemPrompt = null, bool isBuiltIn = false)
    {
        Id = id;
        Name = name;
        SystemPrompt = systemPrompt;
        IsBuiltIn = isBuiltIn;
    }

    public static readonly AIMode Brut = new("voice-to-text", "Brut", isBuiltIn: true);

    public static readonly AIMode Clean = new("clean", "Clean", """
        Tu es un assistant qui nettoie des transcriptions vocales.
        Regles:
        - Supprime les hesitations (euh, hmm, ben, bah, genre, en fait repete)
        - Corrige la ponctuation et les majuscules
        - Garde le sens et le ton exact du message
        - Ne reformule PAS, ne resume PAS
        - Reponds UNIQUEMENT avec le texte corrige, rien d'autre
        """, isBuiltIn: true);

    public static readonly AIMode Formal = new("formal", "Formel", """
        Tu es un assistant qui transforme des transcriptions vocales en texte professionnel.
        Regles:
        - Adopte un ton professionnel et structure
        - Corrige grammaire, ponctuation, majuscules
        - Structure le texte si necessaire (paragraphes)
        - Garde le message original intact
        - Ne change PAS le tutoiement en vouvoiement, ni l'inverse
        - Reponds UNIQUEMENT avec le texte transforme, rien d'autre
        """, isBuiltIn: true);

    public static readonly AIMode Casual = new("casual", "Casual", """
        Tu es un assistant qui nettoie des transcriptions vocales en gardant un ton decontracte.
        Regles:
        - Garde un ton naturel et amical
        - Supprime les hesitations excessives mais garde le naturel
        - Corrige les erreurs evidentes seulement
        - Preserve les expressions familieres
        - Reponds UNIQUEMENT avec le texte nettoye, rien d'autre
        """, isBuiltIn: true);

    public static readonly AIMode Markdown = new("markdown", "Markdown", """
        Tu es un assistant qui convertit des transcriptions vocales en Markdown structure.
        Regles:
        - Utilise des headers (#, ##) si le contenu a une structure
        - Utilise des listes (-, *) pour les enumerations
        - Utilise **gras** pour les points importants
        - Utilise `code` pour les termes techniques
        - Corrige grammaire et ponctuation
        - Reponds UNIQUEMENT avec le texte en Markdown, rien d'autre
        """, isBuiltIn: true);

    public static readonly AIMode Super = new("super", "Super", """
        dynamic
        """, isBuiltIn: true);

    public static readonly AIMode[] BuiltInModes =
    {
        Brut,
        Clean,
        Formal,
        Casual,
        Markdown,
        Super
    };

    public static AIMode FromCustom(CustomModeConfig config) =>
        new(config.Id, config.Name, config.Prompt, isBuiltIn: false);

    public static AIMode? GetBuiltInById(string id) =>
        Array.Find(BuiltInModes, mode => string.Equals(mode.Id, id, StringComparison.OrdinalIgnoreCase));
}
