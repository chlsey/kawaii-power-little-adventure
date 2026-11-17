"""Verify the exploration fallback, measure frozen QA-50, then train the authorized 1,000 episodes."""

from datetime import datetime, timezone
from pathlib import Path
import json
import shutil

from run_local_movement import GAME, checkpoint_hash, load_config, run_waypoints, save
from evaluate_exploration_waypoints import CONFIG, analyze
from test_waypoint_reach import UNITY


TRAIN_CONFIG = GAME/'Training/qa-player-navigation-recovery-qa62-1000.yaml'


def run():
    campaign = GAME/'Logs/qa-navigation-recovery-campaign'/datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
    campaign.mkdir(parents=True)
    config = load_config(TRAIN_CONFIG)
    assert config['max_episodes'] == 1000 and config['seed'] == 520000
    parent = (TRAIN_CONFIG.parent/config['resume_from']).resolve()
    assert checkpoint_hash(parent) == config['resume_sha256']
    metadata = dict(status='verification',qa_id='QA-62',parent=str(parent),parent_sha256=config['resume_sha256'],
        authorized_training_episodes=1000,training_seed_range=[520000,520999],
        frozen_evaluation_seeds=[510000,510099],learning_rate=.00002,
        training_config=str(TRAIN_CONFIG),no_automatic_follow_on_training=True)
    save(campaign/'campaign.json',metadata)
    shutil.copy2(TRAIN_CONFIG,campaign/'training-config.yaml')
    print('Campaign: '+str(campaign),flush=True)
    try:
        # Existing map, clearance, reward, compass and reference-navigation checks.
        run_waypoints(CONFIG,UNITY,'verify',None)
        metadata['status']='frozen-parent-comparison';save(campaign/'campaign.json',metadata)
        output = (CONFIG.parent/load_config(CONFIG)['output_dir']).resolve()
        previous = set(output.glob('*-evaluate'))
        run_waypoints(CONFIG,UNITY,'evaluate',None)
        created = set(output.glob('*-evaluate'))-previous
        assert len(created)==1
        baseline = created.pop()
        analyze(baseline)
        fixed = json.loads((baseline/'exploration-result.json').read_text())
        # Separate the navigator change from subsequent learning; do not train if
        # the frozen baseline loses exits/discoveries on these matched cases.
        assert fixed['exit_discovered'] >= 51 and fixed['exit_completed'] >= 45
        missing = actions = 0
        with (baseline/'evaluation/exit-decisions.jsonl').open(encoding='utf-8') as stream:
            for line in stream:
                row = json.loads(line)
                if row['active']:
                    actions += 1
                    missing += not row['has_waypoint']
        assert missing/actions < 28407/41643
        metadata.update(status='training',frozen_parent_result=str(baseline/'exploration-result.json'),
            frozen_parent_missing_waypoint_fraction=missing/actions)
        save(campaign/'campaign.json',metadata)
        print('Frozen comparison passed; starting exactly 1,000 authorized episodes.',flush=True)
        run_waypoints(TRAIN_CONFIG,UNITY,'train',None)
        assert checkpoint_hash(parent)==config['resume_sha256']
        metadata['status']='complete';save(campaign/'campaign.json',metadata)
    except BaseException as error:
        metadata.update(status='failed',error=str(error))
        save(campaign/'campaign.json',metadata)
        raise


if __name__=='__main__':
    run()
